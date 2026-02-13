using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;

namespace RallyAPI.Delivery.Infrastructure.Repositories;

public sealed class DeliveryRequestRepository : IDeliveryRequestRepository
{
    private readonly DeliveryDbContext _dbContext;

    public DeliveryRequestRepository(DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeliveryRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<DeliveryRequest?> GetByIdWithOffersAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .Include(r => r.RiderOffers)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<DeliveryRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
    }

    public async Task<IReadOnlyList<DeliveryRequest>> GetByStatusAsync(
        DeliveryRequestStatus status,
        CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DeliveryRequest>> GetPendingDispatchAsync(
        DateTime dispatchBefore,
        CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .Where(r => r.Status == DeliveryRequestStatus.PendingDispatch)
            .Where(r => r.DispatchAt.HasValue && r.DispatchAt.Value <= dispatchBefore)
            .OrderBy(r => r.DispatchAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DeliveryRequest request, CancellationToken ct = default)
    {
        await _dbContext.DeliveryRequests.AddAsync(request, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DeliveryRequest request, CancellationToken ct = default)
    {
        _dbContext.DeliveryRequests.Update(request);
        await _dbContext.SaveChangesAsync(ct);
    }

}