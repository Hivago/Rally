using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Events;

public sealed class DeliveryRequestCreatedEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public string OrderNumber { get; }

    public DeliveryRequestCreatedEvent(
        Guid deliveryRequestId,
        Guid orderId,
        string orderNumber)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        OrderNumber = orderNumber;
    }
}