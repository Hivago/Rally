using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Events;

public sealed class DeliveryPickedUpEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public DateTime PickedUpAt { get; }

    public DeliveryPickedUpEvent(
        Guid deliveryRequestId,
        Guid orderId,
        DateTime pickedUpAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        PickedUpAt = pickedUpAt;
    }
}