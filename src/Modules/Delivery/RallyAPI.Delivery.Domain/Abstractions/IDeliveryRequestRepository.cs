using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Domain.Abstractions;

public interface IDeliveryRequestRepository
{
    Task<DeliveryRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeliveryRequest?> GetByIdWithOffersAsync(Guid id, CancellationToken ct = default);
    Task<DeliveryRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryRequest>> GetByStatusAsync(DeliveryRequestStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryRequest>> GetPendingDispatchAsync(DateTime dispatchBefore, CancellationToken ct = default);
    Task AddAsync(DeliveryRequest request, CancellationToken ct = default);
    Task UpdateAsync(DeliveryRequest request, CancellationToken ct = default);
 
}