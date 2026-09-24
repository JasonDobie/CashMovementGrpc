using CashMovement.Application.Models;
using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;

namespace CashMovement.Tests;

public sealed class SqlCashMovementTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture _fixture;

    public SqlCashMovementTests(SqlTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BalanceMath_IsCorrect()
    {
        var account = "TEST-" + Guid.NewGuid().ToString("N");
        await _fixture.CreateAccountAsync(account);
        var service = _fixture.CreateService();

        await service.SubmitAsync(Movement(account, "A-1", 100m), default);
        await service.SubmitAsync(Movement(account, "A-2", -25m), default);
        await service.SubmitAsync(Movement(account, "A-3", 10.50m), default);

        Assert.Equal(85.50m, await service.GetBalanceAsync(account, default));
    }

    [Fact]
    public async Task SameReferenceSubmittedConcurrently_IsCountedOnce()
    {
        var account = "TEST-" + Guid.NewGuid().ToString("N");
        await _fixture.CreateAccountAsync(account);
        var service = _fixture.CreateService();
        var movement = Movement(account, "CONCURRENT-1", 12500m);

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => service.SubmitAsync(movement, default)));

        Assert.Equal(12500m, await service.GetBalanceAsync(account, default));
        Assert.Equal(1, results.Count(x => x.Outcome == SubmissionOutcome.Created));
        Assert.Equal(19, results.Count(x => x.Outcome == SubmissionOutcome.Duplicate));
    }

    [Fact]
    public async Task SameReferenceWithDifferentFields_IsConflictAndDoesNotChangeBalance()
    {
        var account = "TEST-" + Guid.NewGuid().ToString("N");
        await _fixture.CreateAccountAsync(account);
        var service = _fixture.CreateService();

        await service.SubmitAsync(Movement(account, "CONFLICT-1", 100m), default);
        var result = await service.SubmitAsync(Movement(account, "CONFLICT-1", 200m), default);

        Assert.Equal(SubmissionOutcome.Conflict, result.Outcome);
        Assert.Equal(100m, await service.GetBalanceAsync(account, default));
    }

    private static CashMovementEntity Movement(string accountId, string externalRef, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = accountId,
        ExternalRef = externalRef,
        Currency = "ZAR",
        Amount = amount,
        OccurredAtUtc = new DateTime(2024, 7, 15, 10, 42, 31, DateTimeKind.Utc),
        Narration = "Test movement",
        CreatedAtUtc = DateTime.UtcNow
    };
}
