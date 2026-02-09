using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published when delivery is completed successfully.
/// Consumed by Orders module to mark order as delivered.
/// </summary>
public sealed class DeliveryCompletedIntegrationEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public DateTime DeliveredAt { get; }

    public DeliveryCompletedIntegrationEvent(
        Guid deliveryRequestId,
        Guid orderId,
        DateTime deliveredAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        DeliveredAt = deliveredAt;
    }
}