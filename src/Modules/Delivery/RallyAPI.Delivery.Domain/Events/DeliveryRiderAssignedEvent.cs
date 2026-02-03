using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Events;

public sealed class DeliveryRiderAssignedEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public string OrderNumber { get; }
    public FleetType FleetType { get; }
    public Guid? RiderId { get; }
    public string? RiderName { get; }
    public string? RiderPhone { get; }
    public string? TrackingUrl { get; }

    public DeliveryRiderAssignedEvent(
        Guid deliveryRequestId,
        Guid orderId,
        string orderNumber,
        FleetType fleetType,
        Guid? riderId,
        string? riderName,
        string? riderPhone,
        string? trackingUrl)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        OrderNumber = orderNumber;
        FleetType = fleetType;
        RiderId = riderId;
        RiderName = riderName;
        RiderPhone = riderPhone;
        TrackingUrl = trackingUrl;
    }
}