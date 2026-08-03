using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Repositories;

namespace RallyAPI.Orders.Infrastructure.Repositories;

public class RestaurantPayoutExportBatchRepository : IRestaurantPayoutExportBatchRepository
{
    private readonly OrdersDbContext _context;

    public RestaurantPayoutExportBatchRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<RestaurantPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RestaurantPayoutExportBatches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task AddAsync(RestaurantPayoutExportBatch batch, CancellationToken ct = default)
        => await _context.RestaurantPayoutExportBatches.AddAsync(batch, ct);

    public void Update(RestaurantPayoutExportBatch batch)
        => _context.RestaurantPayoutExportBatches.Update(batch);
}
