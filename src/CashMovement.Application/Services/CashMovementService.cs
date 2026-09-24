using CashMovement.Application.Abstractions;
using CashMovement.Application.Models;
using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;

namespace CashMovement.Application.Services;

public sealed class CashMovementService
{
    private readonly ICashMovementRepository _repository;

    public CashMovementService(ICashMovementRepository repository) => _repository = repository;

    public Task<SubmissionResult> SubmitAsync(CashMovementEntity movement, CancellationToken cancellationToken)
    {
        movement.Validate();
        return _repository.AddIdempotentAsync(movement, cancellationToken);
    }

    public Task<decimal> GetBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("AccountId is required.");
        return _repository.GetBalanceAsync(accountId, cancellationToken);
    }

    public IAsyncEnumerable<StatementRow> StreamStatementAsync(
        string accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("AccountId is required.");
        if (fromUtc >= toUtc) throw new ArgumentException("fromUtc must be earlier than toUtc.");
        return _repository.StreamStatementAsync(accountId, fromUtc, toUtc, cancellationToken);
    }
}
