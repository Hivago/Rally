using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Domain.Repositories;

public interface IRestaurantPayoutExportBatchRepository
{
    Task<RestaurantPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RestaurantPayoutExportBatch batch, CancellationToken ct = default);
    void Update(RestaurantPayoutExportBatch batch);

    /// <summary>
    /// Most recent batches first, optionally filtered by status. Backs the admin "which
    /// batches are still open" view — without this, an admin has no way to recover an
    /// exportBatchId once the one-time export response is gone.
    /// </summary>
    Task<IReadOnlyList<RestaurantPayoutExportBatch>> GetRecentAsync(
        PayoutExportBatchStatus? status, int skip, int take, CancellationToken ct = default);
}
