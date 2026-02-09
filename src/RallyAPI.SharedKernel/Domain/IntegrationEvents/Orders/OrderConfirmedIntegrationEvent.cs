using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Orders;

/// <summary>
/// Published when restaurant confirms an order.
/// Consumed by Delivery module to create DeliveryRequest.
/// </summary>
public sealed class OrderConfirmedIntegrationEvent : BaseDomainEvent
{
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public Guid RestaurantId { get; }
    public Guid CustomerId { get; }

    public OrderConfirmedIntegrationEvent(
        Guid orderId,
        string orderNumber,
        Guid restaurantId,
        Guid customerId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        RestaurantId = restaurantId;
        CustomerId = customerId;
    }
}