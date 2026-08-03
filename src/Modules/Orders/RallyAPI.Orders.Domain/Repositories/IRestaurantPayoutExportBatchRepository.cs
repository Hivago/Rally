using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Domain.Repositories;

public interface IRestaurantPayoutExportBatchRepository
{
    Task<RestaurantPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(RestaurantPayoutExportBatch batch, CancellationToken ct = default);
    void Update(RestaurantPayoutExportBatch batch);
}
