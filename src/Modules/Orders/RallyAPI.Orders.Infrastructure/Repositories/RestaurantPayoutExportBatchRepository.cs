using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
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

    public async Task<IReadOnlyList<RestaurantPayoutExportBatch>> GetRecentAsync(
        PayoutExportBatchStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.RestaurantPayoutExportBatches.AsQueryable();
        if (status is not null)
            query = query.Where(b => b.Status == status);

        return await query
            .OrderByDescending(b => b.GeneratedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
