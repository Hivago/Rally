using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Events;

public sealed class DeliveryFailedEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public DeliveryFailureReason Reason { get; }
    public string? Notes { get; }
    public DateTime FailedAt { get; }

    public DeliveryFailedEvent(
        Guid deliveryRequestId,
        Guid orderId,
        DeliveryFailureReason reason,
        string? notes,
        DateTime failedAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        Reason = reason;
        Notes = notes;
        FailedAt = failedAt;
    }
}