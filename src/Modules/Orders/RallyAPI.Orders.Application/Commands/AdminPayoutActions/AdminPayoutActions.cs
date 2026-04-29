using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Payouts;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AdminPayoutActions;

// ============ Pay-now ============

public sealed record PayNowRestaurantPayoutCommand(Guid PayoutId)
    : IRequest<Result<PayNowResponse>>;

public sealed record PayNowResponse(string TransactionReference, string Status);

public sealed class PayNowRestaurantPayoutCommandHandler
    : IRequestHandler<PayNowRestaurantPayoutCommand, Result<PayNowResponse>>
{
    private readonly IPayoutRepository _payouts;
    private readonly IPayoutGateway _gateway;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<PayNowRestaurantPayoutCommandHandler> _log;

    public PayNowRestaurantPayoutCommandHandler(
        IPayoutRepository payouts,
        IPayoutGateway gateway,
        IUnitOfWork uow,
        ILogger<PayNowRestaurantPayoutCommandHandler> log)
    {
        _payouts = payouts; _gateway = gateway; _uow = uow; _log = log;
    }

    public async Task<Result<PayNowResponse>> Handle(PayNowRestaurantPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure<PayNowResponse>(Error.NotFound("Payout", cmd.PayoutId));

        if (payout.Status == Domain.Enums.PayoutStatus.Paid)
            return Result.Failure<PayNowResponse>(Error.Conflict("Payout has already been paid."));

        if (payout.Status != Domain.Enums.PayoutStatus.Pending && payout.Status != Domain.Enums.PayoutStatus.OnHold)
            return Result.Failure<PayNowResponse>(
                Error.Conflict($"Cannot pay-now from {payout.Status} status."));

        var gatewayResult = await _gateway.TriggerAsync(
            payout.OwnerId, payout.NetPayoutAmount, "Restaurant", ct);

        if (!gatewayResult.IsSuccess || gatewayResult.TransactionReference is null)
        {
            _log.LogWarning("Payout gateway failure for {PayoutId}: {Reason}",
                cmd.PayoutId, gatewayResult.FailureReason);
            return Result.Failure<PayNowResponse>(
                Error.Create("Payout.GatewayFailed", gatewayResult.FailureReason ?? "Gateway returned failure."));
        }

        try
        {
            payout.MarkPaidImmediate(gatewayResult.TransactionReference);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PayNowResponse>(Error.Conflict(ex.Message));
        }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation(
            "Admin pay-now: payout {PayoutId} paid via gateway txn {TxnRef}",
            cmd.PayoutId, gatewayResult.TransactionReference);

        return Result.Success(new PayNowResponse(gatewayResult.TransactionReference, payout.Status.ToString()));
    }
}

// ============ Hold ============

public sealed record HoldRestaurantPayoutCommand(Guid PayoutId, string? Reason) : IRequest<Result>;

public sealed class HoldRestaurantPayoutCommandHandler
    : IRequestHandler<HoldRestaurantPayoutCommand, Result>
{
    private readonly IPayoutRepository _payouts;
    private readonly IUnitOfWork _uow;

    public HoldRestaurantPayoutCommandHandler(IPayoutRepository payouts, IUnitOfWork uow)
    { _payouts = payouts; _uow = uow; }

    public async Task<Result> Handle(HoldRestaurantPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("Payout", cmd.PayoutId));

        try { payout.PutOnHold(cmd.Reason); }
        catch (InvalidOperationException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ============ Release hold ============

public sealed record ReleaseHoldRestaurantPayoutCommand(Guid PayoutId) : IRequest<Result>;

public sealed class ReleaseHoldRestaurantPayoutCommandHandler
    : IRequestHandler<ReleaseHoldRestaurantPayoutCommand, Result>
{
    private readonly IPayoutRepository _payouts;
    private readonly IUnitOfWork _uow;

    public ReleaseHoldRestaurantPayoutCommandHandler(IPayoutRepository payouts, IUnitOfWork uow)
    { _payouts = payouts; _uow = uow; }

    public async Task<Result> Handle(ReleaseHoldRestaurantPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("Payout", cmd.PayoutId));

        try { payout.ReleaseHold(); }
        catch (InvalidOperationException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ============ Retry ============

public sealed record RetryRestaurantPayoutCommand(Guid PayoutId) : IRequest<Result>;

public sealed class RetryRestaurantPayoutCommandHandler
    : IRequestHandler<RetryRestaurantPayoutCommand, Result>
{
    private readonly IPayoutRepository _payouts;
    private readonly IUnitOfWork _uow;

    public RetryRestaurantPayoutCommandHandler(IPayoutRepository payouts, IUnitOfWork uow)
    { _payouts = payouts; _uow = uow; }

    public async Task<Result> Handle(RetryRestaurantPayoutCommand cmd, CancellationToken ct)
    {
        var payout = await _payouts.GetByIdAsync(cmd.PayoutId, ct);
        if (payout is null)
            return Result.Failure(Error.NotFound("Payout", cmd.PayoutId));

        try { payout.MarkRetry(); }
        catch (InvalidOperationException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        _payouts.Update(payout);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
