using System.Data;
using CashMovement.Application.Abstractions;
using CashMovement.Application.Models;
using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;
using Microsoft.Data.SqlClient;

namespace CashMovement.Infrastructure.Repositories;

public sealed class SqlCashMovementRepository : ICashMovementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlCashMovementRepository(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<SubmissionResult> AddIdempotentAsync(
        CashMovementEntity movement,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string accountSql = """
                SELECT AccountId, Currency
                FROM dbo.Accounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountId = @AccountId;
                """;

            await using var accountCommand = new SqlCommand(accountSql, connection, transaction);
            accountCommand.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = movement.AccountId;

            await using var accountReader = await accountCommand.ExecuteReaderAsync(cancellationToken);
            if (!await accountReader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException($"Account '{movement.AccountId}' was not found.");

            var accountCurrency = accountReader.GetString(1).Trim();
            await accountReader.DisposeAsync();

            if (!string.Equals(accountCurrency, movement.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Currency '{movement.Currency}' does not match account currency '{accountCurrency}'.");

            const string existingSql = """
                SELECT Id, AccountId, ExternalRef, Currency, Amount, OccurredAtUtc, Narration, CreatedAtUtc
                FROM dbo.CashMovements WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountId = @AccountId AND ExternalRef = @ExternalRef;
                """;

            await using var existingCommand = new SqlCommand(existingSql, connection, transaction);
            existingCommand.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = movement.AccountId;
            existingCommand.Parameters.Add("@ExternalRef", SqlDbType.VarChar, 100).Value = movement.ExternalRef;

            await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existing = ReadMovement(reader);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);

                return Equivalent(existing, movement)
                    ? new SubmissionResult(SubmissionOutcome.Duplicate, existing)
                    : new SubmissionResult(SubmissionOutcome.Conflict, existing);
            }

            await reader.DisposeAsync();

            const string insertSql = """
                INSERT INTO dbo.CashMovements
                    (Id, AccountId, ExternalRef, Currency, Amount, OccurredAtUtc, Narration, CreatedAtUtc)
                VALUES
                    (@Id, @AccountId, @ExternalRef, @Currency, @Amount, @OccurredAtUtc, @Narration, @CreatedAtUtc);
                """;

            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            AddParameters(insertCommand, movement);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            const string balanceSql = """
                UPDATE dbo.Accounts
                SET Balance = Balance + @Amount
                WHERE AccountId = @AccountId;
                """;

            await using var balanceCommand = new SqlCommand(balanceSql, connection, transaction);
            var balanceAmount = balanceCommand.Parameters.Add("@Amount", SqlDbType.Decimal);
            balanceAmount.Precision = 19;
            balanceAmount.Scale = 2;
            balanceAmount.Value = movement.Amount;
            balanceCommand.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = movement.AccountId;

            if (await balanceCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("The account balance could not be updated.");

            await transaction.CommitAsync(cancellationToken);
            return new SubmissionResult(SubmissionOutcome.Created, movement);
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            throw;
        }
    }

    public async Task<decimal> GetBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT Balance
            FROM dbo.Accounts
            WHERE AccountId = @AccountId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = accountId;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null)
            throw new KeyNotFoundException($"Account '{accountId}' was not found.");

        return Convert.ToDecimal(value);
    }

    public async IAsyncEnumerable<StatementRow> StreamStatementAsync(
        string accountId,
        DateTime fromUtc,
        DateTime toUtc,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            WITH Opening AS
            (
                SELECT COALESCE(SUM(Amount), 0) AS OpeningBalance
                FROM dbo.CashMovements
                WHERE AccountId = @AccountId
                  AND OccurredAtUtc < @FromUtc
            )
            SELECT
                m.Id, m.AccountId, m.ExternalRef, m.Currency, m.Amount,
                m.OccurredAtUtc, m.Narration, m.CreatedAtUtc,
                o.OpeningBalance +
                SUM(m.Amount) OVER
                (
                    ORDER BY m.OccurredAtUtc, m.Id
                    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                ) AS RunningBalance
            FROM dbo.CashMovements m
            CROSS JOIN Opening o
            WHERE m.AccountId = @AccountId
              AND m.OccurredAtUtc >= @FromUtc
              AND m.OccurredAtUtc < @ToUtc
            ORDER BY m.OccurredAtUtc, m.Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 0;
        command.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = accountId;
        command.Parameters.Add("@FromUtc", SqlDbType.DateTime2).Value = fromUtc;
        command.Parameters.Add("@ToUtc", SqlDbType.DateTime2).Value = toUtc;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new StatementRow(
                new CashMovementEntity
                {
                    Id = reader.GetGuid(0),
                    AccountId = reader.GetString(1),
                    ExternalRef = reader.GetString(2),
                    Currency = reader.GetString(3).Trim(),
                    Amount = reader.GetDecimal(4),
                    OccurredAtUtc = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                    Narration = reader.GetString(6),
                    CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
                },
                reader.GetDecimal(8));
        }
    }

    private static CashMovementEntity ReadMovement(SqlDataReader reader) => new CashMovementEntity()
    {
        Id = reader.GetGuid(0),
        AccountId = reader.GetString(1),
        ExternalRef = reader.GetString(2),
        Currency = reader.GetString(3).Trim(),
        Amount = reader.GetDecimal(4),
        OccurredAtUtc = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
        Narration = reader.GetString(6),
        CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
    };

    private static void AddParameters(SqlCommand command, CashMovementEntity movement)
    {
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = movement.Id;
        command.Parameters.Add("@AccountId", SqlDbType.VarChar, 50).Value = movement.AccountId;
        command.Parameters.Add("@ExternalRef", SqlDbType.VarChar, 100).Value = movement.ExternalRef;
        command.Parameters.Add("@Currency", SqlDbType.Char, 3).Value = movement.Currency;
        var amount = command.Parameters.Add("@Amount", SqlDbType.Decimal);
        amount.Precision = 19;
        amount.Scale = 2;
        amount.Value = movement.Amount;
        command.Parameters.Add("@OccurredAtUtc", SqlDbType.DateTime2).Value = movement.OccurredAtUtc;
        command.Parameters.Add("@Narration", SqlDbType.NVarChar, 500).Value = movement.Narration;
        command.Parameters.Add("@CreatedAtUtc", SqlDbType.DateTime2).Value = movement.CreatedAtUtc;
    }

    private static bool Equivalent(CashMovementEntity left, CashMovementEntity right) =>
        left.AccountId == right.AccountId &&
        left.ExternalRef == right.ExternalRef &&
        left.Currency == right.Currency &&
        left.Amount == right.Amount &&
        left.OccurredAtUtc == right.OccurredAtUtc &&
        left.Narration == right.Narration;
}
