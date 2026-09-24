using CashMovement.Application.Models;
using CashMovementApplicationService = CashMovement.Application.Services.CashMovementService;
using CashMovement.Contracts;
using CashMovementEntity = CashMovement.Domain.Entities.CashMovement;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Globalization;

namespace CashMovement.GrpcHost.Services;

public sealed class CashMovementGrpcService : CashMovement.Contracts.CashMovementService.CashMovementServiceBase
{
    private readonly CashMovementApplicationService _service;

    public CashMovementGrpcService(CashMovementApplicationService service) => _service = service;

    public override async Task<SubmitMovementResponse> SubmitMovement(
        SubmitMovementRequest request, ServerCallContext context)
    {
        if (!decimal.TryParse(request.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount must be a valid decimal."));

        if (request.OccurredAt is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "occurred_at is required."));

        CashMovementEntity movement;
        try
        {
            movement = new CashMovementEntity
            {
                Id = Guid.NewGuid(),
                AccountId = request.AccountId,
                ExternalRef = request.ExternalRef,
                Currency = request.Currency.ToUpperInvariant(),
                Amount = amount,
                OccurredAtUtc = request.OccurredAt.ToDateTime().ToUniversalTime(),
                Narration = request.Narration,
                CreatedAtUtc = DateTime.UtcNow
            };

            var result = await _service.SubmitAsync(movement, context.CancellationToken);

            if (result.Outcome == SubmissionOutcome.Conflict)
            {
                throw new RpcException(new Status(
                    StatusCode.AlreadyExists,
                    "The account/external reference already exists with different fields."));
            }

            return new SubmitMovementResponse
            {
                Outcome = result.Outcome == SubmissionOutcome.Created
                    ? SubmitMovementResponse.Types.Outcome.Created
                    : SubmitMovementResponse.Types.Outcome.Duplicate,
                Movement = ToProto(result.Movement)
            };
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<BalanceResponse> GetBalance(
        GetBalanceRequest request, ServerCallContext context)
    {
        try
        {
            var balance = await _service.GetBalanceAsync(request.AccountId, context.CancellationToken);
            return new BalanceResponse
            {
                AccountId = request.AccountId,
                Balance = balance.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task ExportStatement(
        ExportStatementRequest request,
        IServerStreamWriter<StatementRow> responseStream,
        ServerCallContext context)
    {
        if (request.FromUtc is null || request.ToUtc is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "from_utc and to_utc are required."));

        try
        {
            await foreach (var row in _service.StreamStatementAsync(
                request.AccountId,
                request.FromUtc.ToDateTime().ToUniversalTime(),
                request.ToUtc.ToDateTime().ToUniversalTime(),
                context.CancellationToken))
            {
                await responseStream.WriteAsync(new StatementRow
                {
                    Movement = ToProto(row.Movement),
                    RunningBalance = row.RunningBalance.ToString("0.00", CultureInfo.InvariantCulture)
                });
            }
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static Movement ToProto(CashMovementEntity movement) => new()
    {
        Id = movement.Id.ToString(),
        ExternalRef = movement.ExternalRef,
        AccountId = movement.AccountId,
        Currency = movement.Currency,
        Amount = movement.Amount.ToString("0.####", CultureInfo.InvariantCulture),
        OccurredAt = Timestamp.FromDateTime(DateTime.SpecifyKind(movement.OccurredAtUtc, DateTimeKind.Utc)),
        Narration = movement.Narration,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(movement.CreatedAtUtc, DateTimeKind.Utc))
    };
}
