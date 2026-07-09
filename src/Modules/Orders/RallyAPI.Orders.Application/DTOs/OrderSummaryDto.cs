using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.DTOs;

/// <summary>
/// Lightweight order summary for lists.
/// </summary>
public sealed record OrderSummaryDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public PaymentStatus PaymentStatus { get; init; }
    public string PaymentStatusDisplay { get; init; } = string.Empty;
    public string RestaurantName { get; init; } = string.Empty;
    public int TotalItems { get; init; }
    public decimal Total { get; init; }
    public string TotalDisplay { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int? EstimatedMinutes { get; init; }
    public string? EstimatedTimeDisplay { get; init; }
}