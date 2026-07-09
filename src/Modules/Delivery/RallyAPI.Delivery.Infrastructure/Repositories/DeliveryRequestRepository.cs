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

    public async Task<DeliveryRequest?> GetActiveByRiderAsync(Guid riderId, CancellationToken ct = default)
    {
        // RiderId is only ever set for own-fleet deliveries, so this implicitly
        // excludes 3PL. Active = assigned through in-transit, before Delivered.
        return await _dbContext.DeliveryRequests
            .Where(r => r.RiderId == riderId)
            .Where(r => r.Status >= DeliveryRequestStatus.RiderAssigned)
            .Where(r => r.Status < DeliveryRequestStatus.Delivered)
            .OrderByDescending(r => r.AssignedAt)
            .FirstOrDefaultAsync(ct);
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

    public async Task<IReadOnlyList<DeliveryRequest>> GetStuckForRedispatchAsync(
        DateTime stuckBefore,
        CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .Where(r => r.Status == DeliveryRequestStatus.Created
                     || r.Status == DeliveryRequestStatus.PendingDispatch
                     || r.Status == DeliveryRequestStatus.SearchingOwnFleet
                     || r.Status == DeliveryRequestStatus.Searching3PL)
            .Where(r => r.RiderId == null)
            // Exclude deliveries that have been handed off to the 3PL provider and are legitimately
            // waiting on its webhook — re-triggering those would create a duplicate provider task.
            // Their timeout is enforced separately by GetThirdPartySearchTimedOutAsync.
            .Where(r => !(r.Status == DeliveryRequestStatus.Searching3PL && r.ThirdPartyDispatchedAt != null))
            // A PendingDispatch request is scheduled for a future DispatchAt (early/predictive
            // dispatch). Don't treat it as "stuck" until that scheduled time has passed — otherwise
            // the 2-min idle net would front-run the prep timer. Its on-time firing is owned by the
            // due-dispatch sweep (GetPendingDispatchAsync). With early dispatch off, DispatchAt is
            // ConfirmedAt, so this is already in the past and behavior is unchanged.
            .Where(r => r.Status != DeliveryRequestStatus.PendingDispatch
                     || (r.DispatchAt.HasValue && r.DispatchAt.Value <= stuckBefore))
            .Where(r => r.UpdatedAt < stuckBefore)
            .OrderBy(r => r.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DeliveryRequest>> GetThirdPartySearchTimedOutAsync(
        DateTime dispatchedBefore,
        CancellationToken ct = default)
    {
        return await _dbContext.DeliveryRequests
            .Where(r => r.Status == DeliveryRequestStatus.Searching3PL)
            .Where(r => r.ThirdPartyDispatchedAt != null && r.ThirdPartyDispatchedAt < dispatchedBefore)
            .OrderBy(r => r.ThirdPartyDispatchedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<DeliveryRequestStatus?> GetCurrentStatusAsync(Guid id, CancellationToken ct = default)
    {
        // AsNoTracking + scalar projection: always hits the DB and never returns a
        // change-tracked instance, so a concurrent rider acceptance is visible even
        // from the long-lived dispatch DbContext.
        var rows = await _dbContext.DeliveryRequests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => (DeliveryRequestStatus?)r.Status)
            .ToListAsync(ct);

        return rows.Count > 0 ? rows[0] : null;
    }

    public async Task<DeliveryRequest?> GetByIdFreshAsync(Guid id, CancellationToken ct = default)
    {
        // Detach any tracked copy of this aggregate (root + its offers) that the long-lived
        // dispatch context is holding, so the reload reflects accept/decline writes committed
        // on other connections and picks up the current xmin. Without this, EF returns the
        // stale identity-map instance and its next write conflicts or clobbers.
        var stale = _dbContext.ChangeTracker.Entries()
            .Where(e => (e.Entity is DeliveryRequest dr && dr.Id == id)
                     || (e.Entity is RiderOffer ro && ro.DeliveryRequestId == id))
            .ToList();
        foreach (var entry in stale)
            entry.State = EntityState.Detached;

        return await _dbContext.DeliveryRequests
            .Include(r => r.RiderOffers)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
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

    public async Task<bool> TryUpdateAsync(DeliveryRequest request, CancellationToken ct = default)
    {
        _dbContext.DeliveryRequests.Update(request);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // xmin changed under us: a rider acceptance (or any other writer) committed on
            // another connection after we loaded this row. The guarded UPDATE matched zero
            // rows, so nothing was written — the assignment stands. Detach so the failed,
            // still-Modified entry can't be replayed on a later SaveChanges from this
            // long-lived dispatch DbContext.
            _dbContext.Entry(request).State = EntityState.Detached;
            return false;
        }
    }
}