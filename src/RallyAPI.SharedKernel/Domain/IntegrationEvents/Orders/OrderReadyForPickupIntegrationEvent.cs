using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Orders;

/// <summary>
/// Published when food is ready for pickup.
/// Consumed by Delivery module to trigger immediate dispatch.
/// </summary>
public sealed class OrderReadyForPickupIntegrationEvent : BaseDomainEvent
{
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public Guid RestaurantId { get; }

    public OrderReadyForPickupIntegrationEvent(
        Guid orderId,
        string orderNumber,
        Guid restaurantId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        RestaurantId = restaurantId;
    }
}