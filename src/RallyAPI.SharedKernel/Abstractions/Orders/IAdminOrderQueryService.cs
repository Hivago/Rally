namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Cross-module service powering the admin Orders Management page.
/// Implemented in Orders.Infrastructure, consumed by Users.Application.
/// </summary>
public interface IAdminOrderQueryService
{
    Task<AdminOrdersPagedResult> SearchAsync(
        AdminOrdersFilter filter,
        CancellationToken cancellationToken = default);

    Task<AdminOrderDetail?> GetDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams matching orders for export. No pagination — caller should consume lazily.
    /// </summary>
    IAsyncEnumerable<AdminOrderExportRow> StreamForExportAsync(
        AdminOrdersFilter filter,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tab filter for the admin orders table.
///   All       — every order in the system (subject to other filters).
///   Active    — Paid/Confirmed/Preparing/ReadyForPickup/PickedUp.
///   Escalated — IsEscalated = true.
///   Failed    — Rejected/Cancelled/Failed/Refunding/Refunded.
/// </summary>
public enum AdminOrdersTab
{
    All = 0,
    Active = 1,
    Escalated = 2,
    Failed = 3
}

public sealed record AdminOrdersFilter(
    AdminOrdersTab Tab,
    string? Search,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public sealed record AdminOrdersPagedResult(
    IReadOnlyList<AdminOrderRow> Items,
    int TotalCount,
    int Page,
    int PageSize,
    AdminOrderTabCounts Counts);

public sealed record AdminOrderTabCounts(
    int All,
    int Active,
    int Escalated,
    int Failed);

public sealed record AdminOrderRow(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string RestaurantName,
    string? RiderName,
    string Status,
    string PaymentStatus,
    string FulfillmentType,
    bool IsEscalated,
    int ItemCount,
    decimal Total,
    string Currency,
    DateTime CreatedAt);

public sealed record AdminOrderDetail(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string StatusDisplay,
    string PaymentStatus,
    string FulfillmentType,
    bool IsEscalated,
    DateTime? EscalatedAt,
    string? EscalationReason,
    int? DelayMinutes,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    Guid RestaurantId,
    string RestaurantName,
    string? RestaurantPhone,
    Guid? RiderId,
    string? RiderName,
    string? RiderPhone,
    string? DeliveryAddress,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal Tax,
    decimal Discount,
    decimal Total,
    string Currency,
    int ItemCount,
    IReadOnlyList<AdminOrderItem> Items,
    string? CancellationReason,
    string? CancellationNotes,
    string? RejectionReason,
    string? SpecialInstructions,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? PreparingAt,
    DateTime? ReadyAt,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt);

public sealed record AdminOrderItem(
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? SpecialInstructions);

public sealed record AdminOrderExportRow(
    string OrderNumber,
    DateTime CreatedAt,
    string Status,
    string PaymentStatus,
    string FulfillmentType,
    string CustomerName,
    string? CustomerPhone,
    string RestaurantName,
    string? RiderName,
    int ItemCount,
    decimal Total,
    string Currency,
    bool IsEscalated,
    string? CancellationReason);
