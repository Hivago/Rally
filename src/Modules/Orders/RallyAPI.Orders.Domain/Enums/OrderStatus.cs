namespace RallyAPI.Orders.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an order.
/// Flow: PAID → CONFIRMED → PREPARING → READY_FOR_PICKUP → PICKED_UP → DELIVERED
/// </summary>
public enum OrderStatus
{
    /// <summary>Order created, awaiting payment</summary>
    Pending = -5,

    /// <summary>Payment completed, waiting for restaurant response</summary>
    Paid = 0,

    /// <summary>Restaurant accepted the order</summary>
    Confirmed = 10,

    /// <summary>Restaurant is preparing the order</summary>
    Preparing = 20,

    /// <summary>Order ready for rider pickup</summary>
    ReadyForPickup = 30,

    /// <summary>Rider has picked up the order</summary>
    PickedUp = 40,

    /// <summary>Order delivered to customer</summary>
    Delivered = 50,

    /// <summary>Restaurant rejected the order</summary>
    Rejected = 90,

    /// <summary>Order was cancelled</summary>
    Cancelled = 100,

    /// <summary>Order failed (delivery, system error, etc.)</summary>
    Failed = 110,

    /// <summary>Refund initiated</summary>
    Refunding = 120,

    /// <summary>Refund completed</summary>
    Refunded = 130
}

/// <summary>
/// Extension methods for OrderStatus
/// </summary>
public static class OrderStatusExtensions
{
    /// <summary>
    /// Checks if order is in a terminal state (no further transitions allowed)
    /// </summary>
    public static bool IsTerminal(this OrderStatus status) => status switch
    {
        OrderStatus.Delivered => true,
        OrderStatus.Rejected => true,
        OrderStatus.Cancelled => true,
        OrderStatus.Failed => true,
        OrderStatus.Refunded => true,
        _ => false
    };

    /// <summary>
    /// Checks if order is in an active state (still in progress)
    /// </summary>
    public static bool IsActive(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => true,
        OrderStatus.Paid => true,
        OrderStatus.Confirmed => true,
        OrderStatus.Preparing => true,
        OrderStatus.ReadyForPickup => true,
        OrderStatus.PickedUp => true,
        _ => false
    };

    /// <summary>
    /// Checks if order can be cancelled from current status
    /// </summary>
    public static bool CanBeCancelled(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => true,    // Before payment completes
        OrderStatus.Paid => true,       // Before restaurant confirms
        OrderStatus.Confirmed => true,  // Before preparation starts
        _ => false
    };

    /// <summary>
    /// Checks if order can be rejected by restaurant
    /// </summary>
    public static bool CanBeRejected(this OrderStatus status) =>
        status == OrderStatus.Paid;

    /// <summary>
    /// Checks if order requires refund on cancellation/rejection
    /// </summary>
    public static bool RequiresRefund(this OrderStatus status) => status switch
    {
        OrderStatus.Rejected => true,
        OrderStatus.Cancelled => true,
        OrderStatus.Failed => true,
        _ => false
    };

    /// <summary>
    /// Gets display name for the status
    /// </summary>
    public static string GetDisplayName(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pending Payment",
        OrderStatus.Paid => "Paid - Awaiting Restaurant",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.Preparing => "Preparing",
        OrderStatus.ReadyForPickup => "Ready for Pickup",
        OrderStatus.PickedUp => "Picked Up",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Rejected => "Rejected by Restaurant",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.Failed => "Failed",
        OrderStatus.Refunding => "Refund in Progress",
        OrderStatus.Refunded => "Refunded",
        _ => "Unknown"
    };
}