using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Commands.ReconcileRestaurantPayouts;

/// <summary>
/// Reconciles an exported ICICI bulk-transfer batch against the bank's Consolidated Status
/// Report. Rows are matched to Processing payouts in the batch by (bank account, IFSC, amount);
/// a matched Success row (with UTR) flips the payout Processing→Paid, a matched
/// Reversed/Rejected row flips it Processing→Failed. Unmatched/ambiguous rows are reported for
/// manual review, never guessed. See specs/icici-manual-payout-export.md section 4a.
/// </summary>
public sealed record ReconcileRestaurantPayoutsCommand(
    Guid ExportBatchId,
    Stream FileStream,
    byte[] FileBytes,
    Guid ReconciledByAdminId) : IRequest<Result<ReconcileRestaurantPayoutsResult>>;

public sealed record ReconcileRestaurantPayoutsResult(
    Guid ExportBatchId,
    int RowsInFile,
    int MarkedPaid,
    int MarkedFailed,
    int AlreadyResolvedSkipped,
    IReadOnlyList<UnresolvedReconciliationRowDto> Unresolved,
    bool BatchFullyReconciled);

/// <summary>A report row that could not be applied automatically — needs admin review.</summary>
public sealed record UnresolvedReconciliationRowDto(
    int RowNumber,
    string BeneficiaryName,
    string AccountNumber,
    decimal Amount,
    string Reason);
