using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.DTOs;

public sealed record DeliveryRequestDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? FleetType { get; init; }
    public decimal QuotedPrice { get; init; }

    // Rider Info
    public RiderInfoDto? Rider { get; init; }

    // Timestamps
    public DateTime CreatedAt { get; init; }
    public DateTime? AssignedAt { get; init; }
    public DateTime? PickedUpAt { get; init; }
    public DateTime? DeliveredAt { get; init; }

    // Distance
    public decimal? DistanceKm { get; init; }
    public int? EstimatedMinutes { get; init; }
}

public sealed record RiderInfoDto
{
    public Guid? RiderId { get; init; }
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? TrackingUrl { get; init; }
    public bool IsOwnFleet { get; init; }
}