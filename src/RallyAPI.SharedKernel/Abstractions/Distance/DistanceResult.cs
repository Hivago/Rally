namespace RallyAPI.SharedKernel.Abstractions.Distance;

/// <summary>
/// Result of distance calculation between two points.
/// </summary>
public sealed record DistanceResult
{
    private DistanceResult() { }

    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Road distance in kilometers.
    /// </summary>
    public decimal DistanceKm { get; private init; }

    /// <summary>
    /// Estimated travel time in minutes.
    /// </summary>
    public int DurationMinutes { get; private init; }

    /// <summary>
    /// Distance in meters (raw from API).
    /// </summary>
    public int DistanceMeters { get; private init; }

    /// <summary>
    /// Duration in seconds (raw from API).
    /// </summary>
    public int DurationSeconds { get; private init; }

    /// <summary>
    /// Human-readable distance (e.g., "5.2 km").
    /// </summary>
    public string? DistanceText { get; private init; }

    /// <summary>
    /// Human-readable duration (e.g., "12 mins").
    /// </summary>
    public string? DurationText { get; private init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    public static DistanceResult Success(
        int distanceMeters,
        int durationSeconds,
        string? distanceText = null,
        string? durationText = null)
    {
        return new DistanceResult
        {
            IsSuccess = true,
            DistanceMeters = distanceMeters,
            DurationSeconds = durationSeconds,
            DistanceKm = Math.Round(distanceMeters / 1000m, 2),
            DurationMinutes = (int)Math.Ceiling(durationSeconds / 60.0),
            DistanceText = distanceText,
            DurationText = durationText
        };
    }

    public static DistanceResult Failure(string errorMessage)
    {
        return new DistanceResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}