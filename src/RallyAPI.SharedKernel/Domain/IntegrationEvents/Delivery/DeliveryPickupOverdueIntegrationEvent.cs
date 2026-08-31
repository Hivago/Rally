using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.SharedKernel.IntegrationEvents.Delivery;

/// <summary>
/// Published when a rider has been assigned to a delivery but has not picked up the order
/// within the recovery service's idle threshold. Consumed by Orders module to escalate the
/// order to admin — no automatic cancellation or reassignment, since a committed rider being
/// slow to update status is not proof they didn't do the work (see DeliveryDispatchRecoveryService
/// 3PL-timeout precedent).
/// </summary>
public sealed class DeliveryPickupOverdueIntegrationEvent : BaseDomainEvent
{
    public Guid DeliveryRequestId { get; }
    public Guid OrderId { get; }
    public TimeSpan IdleFor { get; }

    public DeliveryPickupOverdueIntegrationEvent(
        Guid deliveryRequestId,
        Guid orderId,
        TimeSpan idleFor)
    {
        DeliveryRequestId = deliveryRequestId;
        OrderId = orderId;
        IdleFor = idleFor;
    }
}
