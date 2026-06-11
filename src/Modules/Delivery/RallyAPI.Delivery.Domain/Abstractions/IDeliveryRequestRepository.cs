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
    Task AddAsync(DeliveryRequest request, CancellationToken ct = default);
    Task UpdateAsync(DeliveryRequest request, CancellationToken ct = default);
 
}