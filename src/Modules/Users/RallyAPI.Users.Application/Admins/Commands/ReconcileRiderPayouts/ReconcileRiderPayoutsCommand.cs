using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.ReconcileRiderPayouts;

/// <summary>
/// Reconciles an exported ICICI bulk-transfer batch against the bank's Consolidated Status
/// Report. Mirrors ReconcileRestaurantPayoutsCommand (Orders module) — see that type and
/// specs/icici-manual-payout-export.md section 4a for the full design. Riders have no bank
/// details stored on the ledger itself (export reads them live), so this handler re-fetches
/// current bank details to build the same match key used at export time.
/// </summary>
public sealed record ReconcileRiderPayoutsCommand(
    Guid ExportBatchId,
    Stream FileStream,
    byte[] FileBytes,
    Guid ReconciledByAdminId) : IRequest<Result<ReconcileRiderPayoutsResult>>;

public sealed record ReconcileRiderPayoutsResult(
    Guid ExportBatchId,
    int RowsInFile,
    int MarkedPaid,
    int MarkedFailed,
    int AlreadyResolvedSkipped,
    IReadOnlyList<UnresolvedRiderReconciliationRowDto> Unresolved,
    bool BatchFullyReconciled);

/// <summary>A report row that could not be applied automatically — needs admin review.</summary>
public sealed record UnresolvedRiderReconciliationRowDto(
    int RowNumber,
    string BeneficiaryName,
    string AccountNumber,
    decimal Amount,
    string Reason);
