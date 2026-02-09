using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published when delivery fails.
/// Consumed by Orders module to handle failure.
/// </summary>
public sealed class DeliveryFailedIntegrationEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public string FailureReason { get; }
    public string? FailureNotes { get; }
    public DateTime FailedAt { get; }

    public DeliveryFailedIntegrationEvent(
        Guid deliveryRequestId,
        Guid orderId,
        string failureReason,
        string? failureNotes,
        DateTime failedAt)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        FailureReason = failureReason;
        FailureNotes = failureNotes;
        FailedAt = failedAt;
    }
}