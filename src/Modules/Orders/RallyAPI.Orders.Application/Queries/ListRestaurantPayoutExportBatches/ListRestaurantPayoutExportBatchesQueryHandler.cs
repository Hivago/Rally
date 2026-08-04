using MediatR;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Queries.ListRestaurantPayoutExportBatches;

public sealed class ListRestaurantPayoutExportBatchesQueryHandler
    : IRequestHandler<ListRestaurantPayoutExportBatchesQuery, Result<IReadOnlyList<RestaurantPayoutExportBatchSummaryDto>>>
{
    private readonly IRestaurantPayoutExportBatchRepository _batchRepository;
    private readonly IPayoutRepository _payoutRepository;

    public ListRestaurantPayoutExportBatchesQueryHandler(
        IRestaurantPayoutExportBatchRepository batchRepository,
        IPayoutRepository payoutRepository)
    {
        _batchRepository = batchRepository;
        _payoutRepository = payoutRepository;
    }

    public async Task<Result<IReadOnlyList<RestaurantPayoutExportBatchSummaryDto>>> Handle(
        ListRestaurantPayoutExportBatchesQuery request,
        CancellationToken ct)
    {
        var skip = (Math.Max(request.Page, 1) - 1) * request.PageSize;
        var batches = await _batchRepository.GetRecentAsync(request.Status, skip, request.PageSize, ct);

        var summaries = new List<RestaurantPayoutExportBatchSummaryDto>(batches.Count);
        foreach (var batch in batches)
        {
            // Small per-page fan-out (batches typically hold tens to low-hundreds of rows) —
            // gives the admin a live "how much of this batch is still stuck" view instead of
            // just the static Generated/Reconciled batch status.
            var payouts = await _payoutRepository.GetByExportBatchIdAsync(batch.Id, ct);
            var processing = payouts.Count(p => p.Status == PayoutStatus.Processing);
            var paid = payouts.Count(p => p.Status == PayoutStatus.Paid);
            var failed = payouts.Count(p => p.Status == PayoutStatus.Failed);

            summaries.Add(new RestaurantPayoutExportBatchSummaryDto(
                batch.Id, batch.PeriodStart, batch.PeriodEnd, batch.RowCount, batch.ControlSumTotal,
                batch.Status, batch.GeneratedByAdminId, batch.GeneratedAtUtc,
                batch.ReconciledByAdminId, batch.ReconciledAtUtc,
                processing, paid, failed));
        }

        return Result.Success<IReadOnlyList<RestaurantPayoutExportBatchSummaryDto>>(summaries);
    }
}
