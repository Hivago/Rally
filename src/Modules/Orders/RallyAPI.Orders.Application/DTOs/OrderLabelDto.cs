using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Application.DTOs;

/// <summary>
/// Customer bill / order label — the print-oriented copy that goes ON the packed bag.
/// Unlike the KOT, this DOES carry pricing, the delivery address, distance/ETA, the
/// delivery OTP, and FSSAI licence numbers. Restaurant- or Admin-facing.
/// </summary>
public sealed record OrderLabelDto
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;

    public FulfillmentType FulfillmentType { get; init; }
    public string FulfillmentDisplay { get; init; } = string.Empty;
    public string StatusDisplay { get; init; } = string.Empty;
    public DateTime PlacedAt { get; init; }

    // Customer
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }

    // Restaurant (outlet) identity + legal
    public string RestaurantName { get; init; } = string.Empty;
    public string? RestaurantAddress { get; init; }
    public string? RestaurantFssai { get; init; }

    // Platform (aggregator) legal
    public string? PlatformFssai { get; init; }

    // Delivery context — null for pickup orders
    public string? DeliveryAddress { get; init; }
    public double? DistanceKm { get; init; }
    public int? EstimatedMinutes { get; init; }

    /// <summary>Delivery handoff OTP (pickup code shown to rider). Null until a delivery request exists.</summary>
    public string? DeliveryOtp { get; init; }

    // Lines (with pricing, unlike the KOT)
    public IReadOnlyList<OrderLabelItemDto> Items { get; init; } = Array.Empty<OrderLabelItemDto>();
    public int TotalItems { get; init; }

    // Money
    public string Currency { get; init; } = "INR";
    public decimal SubTotal { get; init; }
    public decimal Tax { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }

    // Notes
    public string? SpecialInstructions { get; init; }
    public bool CutleryRequested { get; init; }
}

/// <summary>A single priced line on the customer bill/label.</summary>
public sealed record OrderLabelItemDto
{
    public string ItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public string? SpecialInstructions { get; init; }
}
