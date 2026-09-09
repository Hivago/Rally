// File: src/Modules/Delivery/RallyAPI.Delivery.Infrastructure/BackgroundServices/RestaurantPinDriftOptions.cs
// Purpose: Configuration for the restaurant-pin-drift detection sweep
// Bound from appsettings.json section "RestaurantPinDrift"

namespace RallyAPI.Delivery.Infrastructure.BackgroundServices;

public sealed class RestaurantPinDriftOptions
{
    public const string SectionName = "RestaurantPinDrift";

    /// <summary>How often (in seconds) the sweep runs. Default: 1800 (30 min) — this is a slow, non-urgent signal.</summary>
    public int CheckIntervalSeconds { get; set; } = 1800;

    /// <summary>Number of most-recent own-fleet pickups examined per restaurant. Default: 10.</summary>
    public int SampleSize { get; set; } = 10;

    /// <summary>A restaurant is flagged only if EVERY sample in the window exceeds this distance from the stored pin. Default: 75.</summary>
    public decimal ThresholdMeters { get; set; } = 75m;
}
