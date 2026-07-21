using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.GenerateRiderPayoutExport;

/// <summary>
/// Generates the weekly ICICI bulk-transfer file for rider payouts. Only Pending payouts
/// for the exact cycle are eligible; each included payout flips to Processing atomically
/// with the batch record, so it can never be picked up by a later export.
/// </summary>
public sealed record GenerateRiderPayoutExportCommand(
    DateTime CycleStartUtc,
    DateTime CycleEndUtc,
    Guid RequestedByAdminId) : IRequest<Result<RiderPayoutExportResult>>;

public sealed record RiderPayoutExportResult(
    Guid ExportBatchId,
    byte[] FileContent,
    string FileName,
    int RowCount,
    decimal ControlSumTotal,
    IReadOnlyList<ExcludedRiderPayoutDto> Excluded);

/// <summary>A Pending payout that could not be included in the export because the rider has no usable bank details.</summary>
public sealed record ExcludedRiderPayoutDto(
    Guid PayoutId,
    Guid RiderId,
    decimal NetPayable,
    string Reason);
