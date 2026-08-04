using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ManuallyResolveRestaurantPayout;

public enum ManualPayoutResolutionOutcome
{
    Paid,
    Failed
}

/// <summary>
/// Escape hatch for a Processing payout the automatic reconcile matcher can't resolve on its
/// own — an ambiguous match, a rider/owner whose bank details drifted, or a row the bank's
/// report never covered. The admin has manually verified the outcome (e.g. checked the ICICI
/// portal directly) and asserts it here. Same trust boundary as automatic reconciliation
/// (Super Admin only, duplicate-UTR checked, structured audit log) — see
/// specs/icici-manual-payout-export.md section 4a. Never a substitute for the automatic path;
/// use only when the matcher genuinely can't decide.
/// </summary>
public sealed record ManuallyResolveRestaurantPayoutCommand(
    Guid PayoutId,
    ManualPayoutResolutionOutcome Outcome,
    string? TransactionReference,
    string Reason,
    Guid ResolvedByAdminId) : IRequest<Result>;
