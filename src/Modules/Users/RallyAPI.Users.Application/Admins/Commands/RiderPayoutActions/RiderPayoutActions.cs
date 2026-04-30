using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.RiderPayoutActions;

public sealed record RiderPayNowResponse(string TransactionReference, string Status);

public sealed record PayNowRiderPayoutCommand(Guid PayoutId)
    : IRequest<Result<RiderPayNowResponse>>;

public sealed class PayNowRiderPayoutCommandHandler
    : IRequestHandler<PayNowRiderPayoutCommand, Result<RiderPayNowResponse>>
{
    private readonly IRiderPayoutLedgerRepository _payouts;
    private readonly IPayoutGateway _gateway;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<PayNowRiderPayoutCommandHandler> _log;

    public PayNowRiderPayoutCommandHandler(
        IRiderPayoutLedgerRepository payouts,
        IPayoutGateway gateway,
        IUnitOfWork uow,
        ILogger<PayNowRiderPayoutCommandHandler> log)
    {
        _payouts = payouts;
        _gateway = gateway;
        _uow = uow;
        _log = log;
    }

    public async Task<Result<RiderPayNowResponse>> Handle(
        PayNowRiderPayoutCommand cmd,
        CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure<RiderPayNowResponse>(Error.NotFound("RiderPayout", cmd.PayoutId));

        if (payout.Status == RiderPayoutStatus.Paid)
            return Result.Failure<RiderPayNowResponse>(Error.Conflict("Rider payout has already been paid."));

        if (payout.Status != RiderPayoutStatus.Pending && payout.Status != RiderPayoutStatus.OnHold)
            return Result.Failure<RiderPayNowResponse>(
                Error.Conflict($"Cannot pay-now from {payout.Status} status."));

        PayoutResult gatewayResult = await _gateway.TriggerAsync(
            payout.RiderId,
            payout.NetPayable,
            "Rider",
            ct);

        if (!gatewayResult.IsSuccess)
        {
            _log.LogWarning("Rider payout gateway failure for {PayoutId}: {Reason}",
                cmd.PayoutId,
                gatewayResult.FailureReason);

            return Result.Failure<RiderPayNowResponse>(
                Error.Create("RiderPayout.GatewayFailed", gatewayResult.FailureReason ?? "Gateway returned failure."));
        }

        string transactionReference = $"STUB-RIDER-{Guid.NewGuid():N}";

        try
        {
            payout.MarkPaidImmediate(transactionReference);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RiderPayNowResponse>(Error.Conflict(ex.Message));
        }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation(
            "Admin pay-now: rider payout {PayoutId} paid via gateway txn {TxnRef}",
            cmd.PayoutId,
            transactionReference);

        return Result.Success(new RiderPayNowResponse(transactionReference, payout.Status.ToString()));
    }
}

public sealed record HoldRiderPayoutCommand(Guid PayoutId, string? Reason) : IRequest<Result>;

public sealed class HoldRiderPayoutCommandHandler
    : IRequestHandler<HoldRiderPayoutCommand, Result>
{
    private readonly IRiderPayoutLedgerRepository _payouts;
    private readonly IUnitOfWork _uow;

    public HoldRiderPayoutCommandHandler(IRiderPayoutLedgerRepository payouts, IUnitOfWork uow)
    {
        _payouts = payouts;
        _uow = uow;
    }

    public async Task<Result> Handle(HoldRiderPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("RiderPayout", cmd.PayoutId));

        if (payout.Status == RiderPayoutStatus.OnHold)
            return Result.Failure(Error.Conflict("Rider payout is already on hold."));

        if (payout.Status == RiderPayoutStatus.Paid)
            return Result.Failure(Error.Conflict("Rider payout has already been paid."));

        try
        {
            payout.PutOnHold(cmd.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict(ex.Message));
        }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record ReleaseHoldRiderPayoutCommand(Guid PayoutId) : IRequest<Result>;

public sealed class ReleaseHoldRiderPayoutCommandHandler
    : IRequestHandler<ReleaseHoldRiderPayoutCommand, Result>
{
    private readonly IRiderPayoutLedgerRepository _payouts;
    private readonly IUnitOfWork _uow;

    public ReleaseHoldRiderPayoutCommandHandler(IRiderPayoutLedgerRepository payouts, IUnitOfWork uow)
    {
        _payouts = payouts;
        _uow = uow;
    }

    public async Task<Result> Handle(ReleaseHoldRiderPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("RiderPayout", cmd.PayoutId));

        try
        {
            payout.ReleaseHold();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict(ex.Message));
        }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record RetryRiderPayoutCommand(Guid PayoutId) : IRequest<Result>;

public sealed class RetryRiderPayoutCommandHandler
    : IRequestHandler<RetryRiderPayoutCommand, Result>
{
    private readonly IRiderPayoutLedgerRepository _payouts;
    private readonly IUnitOfWork _uow;

    public RetryRiderPayoutCommandHandler(IRiderPayoutLedgerRepository payouts, IUnitOfWork uow)
    {
        _payouts = payouts;
        _uow = uow;
    }

    public async Task<Result> Handle(RetryRiderPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("RiderPayout", cmd.PayoutId));

        try
        {
            payout.MarkRetry();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Conflict(ex.Message));
        }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
