namespace RallyAPI.SharedKernel.Abstractions.Distance;

/// <summary>
/// Service for calculating road distance and travel time.
/// </summary>
public interface IDistanceCalculator
{
    /// <summary>
    /// Calculates road distance between two points.
    /// </summary>
    Task<DistanceResult> GetDistanceAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates distances from one origin to multiple destinations.
    /// Useful for finding nearest riders.
    /// </summary>
    Task<IReadOnlyList<DistanceResult>> GetDistancesAsync(
        double originLat,
        double originLng,
        IReadOnlyList<(double lat, double lng)> destinations,
        CancellationToken ct = default);
}