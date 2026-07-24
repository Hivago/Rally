using MediatR;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.AdminPayoutActions;

// Pay-now (gateway-triggered) is intentionally not implemented. Restaurant payouts are
// settled manually via ICICI bulk transfer — "Paid" is only ever set by reconciling the
// bank statement (see ReconcileRestaurantPayoutsCommand), never by an admin button that
// claims money moved without a real bank-issued UTR.

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
