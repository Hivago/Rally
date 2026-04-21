using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.DTOs;

/// <summary>
/// Complete order data transfer object.
/// </summary>
public sealed record OrderDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;

    // Customer
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }

    // Restaurant
    public Guid RestaurantId { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string? RestaurantPhone { get; init; }

    // Fulfillment
    public FulfillmentType FulfillmentType { get; init; }

    // Status
    public OrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public PaymentStatus PaymentStatus { get; init; }
    public string PaymentStatusDisplay { get; init; } = string.Empty;

    // Items
    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
    public int TotalItems { get; init; }

    // Pricing
    public OrderPricingDto Pricing { get; init; } = new();

    // Delivery (null for pickup orders)
    public DeliveryInfoDto? DeliveryInfo { get; init; }

    // Timestamps
    public DateTime CreatedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? PreparingAt { get; init; }
    public DateTime? ReadyAt { get; init; }
    public DateTime? PickedUpAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }

    // Cancellation
    public CancellationReason? CancellationReason { get; init; }
    public string? CancellationNotes { get; init; }

    // Additional
    public string? SpecialInstructions { get; init; }

    // Computed
    public bool CanCancel { get; init; }
    public bool CanModify { get; init; }
    public IReadOnlyList<OrderStatus> AvailableTransitions { get; init; } = Array.Empty<OrderStatus>();

    // Payment Reference
    public string? PaymentId { get; init; }
    public string? PaymentTransactionId { get; init; }

    // Delivery Quote Reference
    public string? DeliveryQuoteId { get; init; }

    // Rejection
    public string? RejectionReason { get; init; }
    public DateTime? RejectedAt { get; init; }
}