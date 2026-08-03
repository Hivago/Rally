using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderPayoutExportBatchRepository
{
    Task<RiderPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RiderPayoutExportBatch batch, CancellationToken ct = default);
    void Update(RiderPayoutExportBatch batch);
}
