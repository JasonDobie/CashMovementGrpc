namespace CashMovement.Domain.Entities;

public sealed class CashMovement
{
    public Guid Id { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public string ExternalRef { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Narration { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AccountId)) throw new ArgumentException("AccountId is required.");
        if (string.IsNullOrWhiteSpace(ExternalRef)) throw new ArgumentException("ExternalRef is required.");
        if (string.IsNullOrWhiteSpace(Currency) || Currency.Length != 3) throw new ArgumentException("Currency must be a 3-letter ISO code.");
        if (Amount == 0) throw new ArgumentException("Amount cannot be zero.");
        if (string.IsNullOrWhiteSpace(Narration)) throw new ArgumentException("Narration is required.");
    }
}
