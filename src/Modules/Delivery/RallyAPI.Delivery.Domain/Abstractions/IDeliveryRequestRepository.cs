using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Domain.Abstractions;

public interface IDeliveryRequestRepository
{
    Task<DeliveryRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeliveryRequest?> GetByIdWithOffersAsync(Guid id, CancellationToken ct = default);
    Task<DeliveryRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Returns the rider's current in-progress own-fleet delivery (assigned through
    /// in-transit, before Delivered/terminal), or null if the rider has none.
    /// Used to forward live GPS to the customer.
    /// </summary>
    Task<DeliveryRequest?> GetActiveByRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryRequest>> GetByStatusAsync(DeliveryRequestStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryRequest>> GetPendingDispatchAsync(DateTime dispatchBefore, CancellationToken ct = default);

    /// <summary>
    /// Returns requests wedged in a pre-assignment state (Created / PendingDispatch /
    /// SearchingOwnFleet / Searching3PL) with no rider assigned that have been idle
    /// (no status change) since before <paramref name="stuckBefore"/>. Used by the
    /// dispatch recovery service to re-trigger dispatch for orders whose inline
    /// dispatch was interrupted and never retried.
    /// </summary>
    Task<IReadOnlyList<DeliveryRequest>> GetStuckForRedispatchAsync(DateTime stuckBefore, CancellationToken ct = default);

    /// <summary>
    /// Reads the delivery's CURRENT status straight from the database, bypassing the
    /// change-tracker identity map. The inline dispatcher holds one DbContext for the
    /// whole dispatch run, so a normal (tracking) reload returns its own stale copy and
    /// misses a rider acceptance committed on another connection. Returns null if the
    /// request no longer exists.
    /// </summary>
    Task<DeliveryRequestStatus?> GetCurrentStatusAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(DeliveryRequest request, CancellationToken ct = default);
    Task UpdateAsync(DeliveryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Persists the request and returns <c>true</c> if the write committed. Returns
    /// <c>false</c> if a concurrent update bumped the row's concurrency token (xmin) and
    /// the write was rejected — i.e. a rider acceptance committed on another connection
    /// between the caller's last status probe and this write. The inline dispatcher uses
    /// this for terminal "Failed" writes so it honors the assignment instead of clobbering
    /// it back to Failed. Unlike <see cref="UpdateAsync"/>, a lost race is a normal,
    /// expected outcome here — not an exception the caller must handle.
    /// </summary>
    Task<bool> TryUpdateAsync(DeliveryRequest request, CancellationToken ct = default);
}