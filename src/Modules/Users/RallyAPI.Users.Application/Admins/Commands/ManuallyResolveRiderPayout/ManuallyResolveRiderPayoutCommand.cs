using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ManuallyResolveRiderPayout;

public enum ManualPayoutResolutionOutcome
{
    Paid,
    Failed
}

/// <summary>
/// Escape hatch for a Processing rider payout the automatic reconcile matcher can't resolve —
/// mirrors ManuallyResolveRestaurantPayoutCommand (Orders module). See that type and
/// specs/icici-manual-payout-export.md section 4a for the full rationale and trust boundary.
/// </summary>
public sealed record ManuallyResolveRiderPayoutCommand(
    Guid PayoutId,
    ManualPayoutResolutionOutcome Outcome,
    string? TransactionReference,
    string Reason,
    Guid ResolvedByAdminId) : IRequest<Result>;
