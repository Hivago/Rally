namespace RallyAPI.Infrastructure.GoogleMaps;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Google Maps API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Distance Matrix API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://maps.googleapis.com/maps/api";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Enable/disable the service.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Travel mode: driving, walking, bicycling, transit.
    /// </summary>
    public string TravelMode { get; set; } = "driving";

    /// <summary>
    /// Region bias for results.
    /// </summary>
    public string Region { get; set; } = "in";
}