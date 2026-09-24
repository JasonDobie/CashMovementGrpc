using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;

namespace CashMovement.Application.Models;

public enum SubmissionOutcome
{
    Created,
    Duplicate,
    Conflict
}

public sealed record SubmissionResult(SubmissionOutcome Outcome, CashMovementEntity Movement);
