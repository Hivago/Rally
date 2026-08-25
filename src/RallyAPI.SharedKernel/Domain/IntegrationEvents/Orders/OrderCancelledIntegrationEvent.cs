using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Orders;

/// <summary>
/// Raised when an order is cancelled, for any reason, at any point in its lifecycle.
/// Consumed by the Delivery module to cancel the in-flight DeliveryRequest (if any)
/// and release the assigned rider so they aren't left permanently pinned to a dead order.
/// </summary>
public sealed class OrderCancelledIntegrationEvent : BaseDomainEvent
{
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public string Reason { get; }

    public OrderCancelledIntegrationEvent(Guid orderId, string orderNumber, string reason)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Reason = reason;
    }
}
