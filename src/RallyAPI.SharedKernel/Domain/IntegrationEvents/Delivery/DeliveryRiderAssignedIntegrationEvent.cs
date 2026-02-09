using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published when a rider is assigned to delivery.
/// Consumed by Orders module to update order with rider info.
/// </summary>
public sealed class DeliveryRiderAssignedIntegrationEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public bool IsOwnFleet { get; }
    public Guid? RiderId { get; }
    public string? RiderName { get; }
    public string? RiderPhone { get; }
    public string? TrackingUrl { get; }

    public DeliveryRiderAssignedIntegrationEvent(
        Guid deliveryRequestId,
        Guid orderId,
        string orderNumber,
        bool isOwnFleet,
        Guid? riderId,
        string? riderName,
        string? riderPhone,
        string? trackingUrl)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        OrderNumber = orderNumber;
        IsOwnFleet = isOwnFleet;
        RiderId = riderId;
        RiderName = riderName;
        RiderPhone = riderPhone;
        TrackingUrl = trackingUrl;
    }
}