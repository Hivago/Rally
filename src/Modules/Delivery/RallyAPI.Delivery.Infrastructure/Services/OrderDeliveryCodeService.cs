using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Delivery.Infrastructure.Services;

/// <summary>
/// Delivery-module implementation of the cross-module delivery-code read.
/// Surfaces the pickup/drop OTPs of an order's delivery request to other modules
/// (e.g. the Orders customer bill/label) without exposing Delivery internals.
/// </summary>
public sealed class OrderDeliveryCodeService : IOrderDeliveryCodeService
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;

    public OrderDeliveryCodeService(IDeliveryRequestRepository deliveryRequestRepository)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
    }

    public async Task<OrderDeliveryCodes?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var request = await _deliveryRequestRepository.GetByOrderIdAsync(orderId, ct);
        if (request is null)
            return null;

        return new OrderDeliveryCodes(request.PickupCode, request.DropCode);
    }
}
