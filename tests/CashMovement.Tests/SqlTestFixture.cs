using CashMovement.Infrastructure;
using Microsoft.Data.SqlClient;

namespace CashMovement.Tests;

public sealed class SqlTestFixture : Xunit.IAsyncLifetime
{
    public const string ConnectionString =
        @"Server=.\SQLEXPRESS;Database=CashMovements;Integrated Security=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public CashMovement.Application.Services.CashMovementService CreateService() =>
        new(new CashMovement.Infrastructure.Repositories.SqlCashMovementRepository(
            new SqlConnectionFactory(ConnectionString)));

    public async Task CreateAccountAsync(string accountId, string currency = "ZAR")
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "INSERT INTO dbo.Accounts(AccountId, Currency, Balance) VALUES (@AccountId, @Currency, 0);",
            connection);
        command.Parameters.Add("@AccountId", System.Data.SqlDbType.VarChar, 50).Value = accountId;
        command.Parameters.Add("@Currency", System.Data.SqlDbType.Char, 3).Value = currency;
        await command.ExecuteNonQueryAsync();
    }
}
