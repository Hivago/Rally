using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderPayoutExportBatchRepository
{
    Task<RiderPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RiderPayoutExportBatch batch, CancellationToken ct = default);
    void Update(RiderPayoutExportBatch batch);

    /// <summary>
    /// Most recent batches first, optionally filtered by status. Backs the admin "which
    /// batches are still open" view — without this, an admin has no way to recover an
    /// exportBatchId once the one-time export response is gone.
    /// </summary>
    Task<IReadOnlyList<RiderPayoutExportBatch>> GetRecentAsync(
        PayoutExportBatchStatus? status, int skip, int take, CancellationToken ct = default);
}
