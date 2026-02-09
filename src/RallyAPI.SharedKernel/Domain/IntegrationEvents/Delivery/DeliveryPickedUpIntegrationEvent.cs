using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published when rider picks up the order.
/// Consumed by Orders module to update status.
/// </summary>
public sealed class DeliveryPickedUpIntegrationEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public DateTime PickedUpAt { get; }

    public DeliveryPickedUpIntegrationEvent(
        Guid deliveryRequestId,
        Guid orderId,
        DateTime pickedUpAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        PickedUpAt = pickedUpAt;
    }
}