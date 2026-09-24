using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;
using CashMovement.Application.Models;

namespace CashMovement.Application.Abstractions;

public interface ICashMovementRepository
{
    Task<SubmissionResult> AddIdempotentAsync(CashMovementEntity movement, CancellationToken cancellationToken);
    Task<decimal> GetBalanceAsync(string accountId, CancellationToken cancellationToken);
    IAsyncEnumerable<StatementRow> StreamStatementAsync(
        string accountId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}

public sealed record StatementRow(CashMovementEntity Movement, decimal RunningBalance);
