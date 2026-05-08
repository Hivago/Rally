// RallyAPI.Pricing.Domain/ValueObjects/PricingContext.cs
using RallyAPI.Pricing.Domain.Enums;

namespace RallyAPI.Pricing.Domain.ValueObjects;

public class PricingContext
{
    // Location
    public double RestaurantLatitude { get; init; }
    public double RestaurantLongitude { get; init; }
    public double CustomerLatitude { get; init; }
    public double CustomerLongitude { get; init; }

    // Pincodes (needed for 3PL)
    public string? PickupPincode { get; init; }
    public string? DropPincode { get; init; }
    public string? City { get; init; }

    // Time
    public DateTime OrderTime { get; init; }
    public DayOfWeek DayOfWeek { get; init; }

    // Order Info
    public decimal OrderSubtotal { get; init; }
    public int ItemCount { get; init; }
    public decimal? OrderWeight { get; init; }
    public Guid RestaurantId { get; init; }
    public Guid? CustomerId { get; init; }

    // External Factors
    public WeatherCondition? Weather { get; init; }
    public int? CurrentOrdersPerHour { get; init; }

    // Optional
    public string? PromoCode { get; init; }

    // 3PL Quote (set by rule)
    public DeliveryQuote? ThirdPartyQuote { get; private set; }

    // Calculated by the query handler using IDistanceCalculator (Google Maps + Haversine fallback)
    public double DistanceKm { get; init; }

    public void SetThirdPartyQuote(
        string quoteId,
        string providerName,
        decimal price,
        int estimatedMinutes)
    {
        ThirdPartyQuote = DeliveryQuote.CreateWithExpiry(
            quoteId,
            providerName,
            price,
            estimatedMinutes);
    }

}