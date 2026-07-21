using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Commands.RiderPayoutActions;

// Pay-now (gateway-triggered) is intentionally not implemented. Rider payouts are settled
// manually via ICICI bulk transfer — "Paid" is only ever set by reconciling the bank
// statement (see ReconcileRiderPayoutsCommand), never by an admin button that claims money
// moved without a real bank-issued UTR.

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
