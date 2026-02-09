namespace RallyAPI.Delivery.Application.DTOs;

public sealed record RiderOfferDto
{
    public Guid OfferId { get; init; }
    public Guid DeliveryRequestId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;

    // Restaurant Info
    public string RestaurantName { get; init; } = string.Empty;
    public string PickupAddress { get; init; } = string.Empty;
    public double PickupLatitude { get; init; }
    public double PickupLongitude { get; init; }

    // Customer Info
    public string DropAddress { get; init; } = string.Empty;
    public double DropLatitude { get; init; }
    public double DropLongitude { get; init; }

    // Distance & Earnings
    public decimal DistanceToPickupKm { get; init; }
    public decimal DistanceToDropKm { get; init; }
    public decimal TotalDistanceKm { get; init; }
    public decimal Earnings { get; init; }

    // Timing
    public int ExpiresInSeconds { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsFoodReady { get; init; }
}