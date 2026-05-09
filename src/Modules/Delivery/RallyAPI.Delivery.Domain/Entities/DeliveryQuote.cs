using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Delivery.Domain.Entities;

/// <summary>
/// Stores delivery quotes generated at checkout.
/// </summary>
public sealed class DeliveryQuote : BaseEntity
{
    private DeliveryQuote() { }

    // Location
    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public string PickupPincode { get; private set; } = string.Empty;
    public double DropLatitude { get; private set; }
    public double DropLongitude { get; private set; }
    public string DropPincode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;

    // Order Info
    public decimal OrderAmount { get; private set; }
    public Guid? RestaurantId { get; private set; }

    // Distance & Pricing
    public decimal DistanceKm { get; private set; }
    public decimal BaseFee { get; private set; }
    public decimal FinalFee { get; private set; }
    public decimal SurgeMultiplier { get; private set; } = 1.0m;
    public string? SurgeReason { get; private set; }
    public int EstimatedMinutes { get; private set; }

    // Fleet Info
    public FleetType FleetType { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string? ProviderQuoteId { get; private set; }

    // Validity
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public Guid? UsedForOrderId { get; private set; }

    // Breakdown (JSON stored)
    public string? BreakdownJson { get; private set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsExpired && !IsUsed;

    public static DeliveryQuote CreateOwnFleet(
        Guid id,
        double pickupLat, double pickupLng, string pickupPincode,
        double dropLat, double dropLng, string dropPincode,
        string city,
        decimal orderAmount,
        Guid? restaurantId,
        decimal distanceKm,
        decimal baseFee,
        decimal finalFee,
        int estimatedMinutes,
        DateTime expiresAt,
        string? breakdownJson = null,
        decimal surgeMultiplier = 1.0m,
        string? surgeReason = null)
    {
        return new DeliveryQuote
        {
            Id = id,
            PickupLatitude = pickupLat,
            PickupLongitude = pickupLng,
            PickupPincode = pickupPincode,
            DropLatitude = dropLat,
            DropLongitude = dropLng,
            DropPincode = dropPincode,
            City = city,
            OrderAmount = orderAmount,
            RestaurantId = restaurantId,
            DistanceKm = distanceKm,
            BaseFee = baseFee,
            FinalFee = finalFee,
            EstimatedMinutes = estimatedMinutes,
            SurgeMultiplier = surgeMultiplier,
            SurgeReason = surgeReason,
            FleetType = FleetType.OwnFleet,
            ProviderName = "Rally",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = NormalizeToUtc(expiresAt),
            BreakdownJson = breakdownJson
        };
    }

    public static DeliveryQuote CreateThirdParty(
        Guid id,
        double pickupLat, double pickupLng, string pickupPincode,
        double dropLat, double dropLng, string dropPincode,
        string city,
        decimal orderAmount,
        Guid? restaurantId,
        decimal price,
        int estimatedMinutes,
        string providerName,
        string providerQuoteId,
        DateTime expiresAt)
    {
        return new DeliveryQuote
        {
            Id = id,
            PickupLatitude = pickupLat,
            PickupLongitude = pickupLng,
            PickupPincode = pickupPincode,
            DropLatitude = dropLat,
            DropLongitude = dropLng,
            DropPincode = dropPincode,
            City = city,
            OrderAmount = orderAmount,
            RestaurantId = restaurantId,
            DistanceKm = 0, // Unknown for 3PL
            BaseFee = price,
            FinalFee = price,
            EstimatedMinutes = estimatedMinutes,
            FleetType = FleetType.ThirdParty,
            ProviderName = providerName,
            ProviderQuoteId = providerQuoteId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = NormalizeToUtc(expiresAt)
        };
    }

    /// <summary>
    /// Postgres `timestamp with time zone` rejects DateTime.Kind=Unspecified or Local.
    /// Coerce any caller's value to UTC so we never crash on save.
    /// </summary>
    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public void MarkAsUsed(Guid orderId)
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
        UsedForOrderId = orderId;
    }
}