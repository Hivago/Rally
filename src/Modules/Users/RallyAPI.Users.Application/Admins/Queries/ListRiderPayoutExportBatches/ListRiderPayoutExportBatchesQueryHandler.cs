using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Queries.ListRiderPayoutExportBatches;

public sealed class ListRiderPayoutExportBatchesQueryHandler
    : IRequestHandler<ListRiderPayoutExportBatchesQuery, Result<IReadOnlyList<RiderPayoutExportBatchSummaryDto>>>
{
    private readonly IRiderPayoutExportBatchRepository _batchRepository;
    private readonly IRiderPayoutLedgerRepository _ledgerRepository;

    public ListRiderPayoutExportBatchesQueryHandler(
        IRiderPayoutExportBatchRepository batchRepository,
        IRiderPayoutLedgerRepository ledgerRepository)
    {
        _batchRepository = batchRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<IReadOnlyList<RiderPayoutExportBatchSummaryDto>>> Handle(
        ListRiderPayoutExportBatchesQuery request,
        CancellationToken ct)
    {
        var skip = (Math.Max(request.Page, 1) - 1) * request.PageSize;
        var batches = await _batchRepository.GetRecentAsync(request.Status, skip, request.PageSize, ct);

        var summaries = new List<RiderPayoutExportBatchSummaryDto>(batches.Count);
        foreach (var batch in batches)
        {
            var payouts = await _ledgerRepository.GetByExportBatchIdAsync(batch.Id, ct);
            var processing = payouts.Count(p => p.Status == RiderPayoutStatus.Processing);
            var paid = payouts.Count(p => p.Status == RiderPayoutStatus.Paid);
            var failed = payouts.Count(p => p.Status == RiderPayoutStatus.Failed);

            summaries.Add(new RiderPayoutExportBatchSummaryDto(
                batch.Id, batch.PeriodStart, batch.PeriodEnd, batch.RowCount, batch.ControlSumTotal,
                batch.Status, batch.GeneratedByAdminId, batch.GeneratedAtUtc,
                batch.ReconciledByAdminId, batch.ReconciledAtUtc,
                processing, paid, failed));
        }

        return Result.Success<IReadOnlyList<RiderPayoutExportBatchSummaryDto>>(summaries);
    }
}
