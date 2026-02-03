using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Events;

public sealed class DeliveryCompletedEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public DateTime DeliveredAt { get; }

    public DeliveryCompletedEvent(
        Guid deliveryRequestId,
        Guid orderId,
        DateTime deliveredAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        DeliveredAt = deliveredAt;
    }
}