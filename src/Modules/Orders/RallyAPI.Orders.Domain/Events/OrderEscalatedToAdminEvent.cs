// File: src/Modules/Orders/RallyAPI.Orders.Domain/Events/OrderEscalatedToAdminEvent.cs
// Purpose: Domain event raised when an order is escalated to admin due to restaurant non-response
// NOTE: Must be a class (not record) because BaseDomainEvent is a class.

using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Events;

public sealed class OrderEscalatedToAdminEvent : BaseDomainEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; }
    public Guid RestaurantId { get; init; }
    public string EscalationReason { get; init; }
    public DateTime EscalatedAt { get; init; }

    public OrderEscalatedToAdminEvent(
        Guid orderId,
        string orderNumber,
        Guid restaurantId,
        string escalationReason,
        DateTime escalatedAt)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        RestaurantId = restaurantId;
        EscalationReason = escalationReason;
        EscalatedAt = escalatedAt;
    }
}