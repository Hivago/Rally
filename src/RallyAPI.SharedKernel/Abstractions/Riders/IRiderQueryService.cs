namespace RallyAPI.SharedKernel.Abstractions.Riders;

/// <summary>
/// Service for querying rider information.
/// Implemented by Users module, consumed by Delivery module.
/// </summary>
public interface IRiderQueryService
{
    /// <summary>
    /// Gets riders who are:
    /// - Online (IsOnline = true)
    /// - Active (IsActive = true, KYC verified)
    /// - Available (CurrentDeliveryId = null)
    /// - Within the specified radius of the location
    /// - Have recent location update (within last 5 minutes)
    /// 
    /// Results are sorted by distance to the specified location.
    /// </summary>
    /// <param name="latitude">Pickup location latitude</param>
    /// <param name="longitude">Pickup location longitude</param>
    /// <param name="radiusKm">Search radius in kilometers</param>
    /// <param name="maxResults">Maximum number of riders to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of available riders sorted by distance</returns>
    Task<IReadOnlyList<AvailableRider>> GetAvailableRidersAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int maxResults = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if at least one rider is available in the area.
    /// Used at checkout to determine which fleet to quote.
    /// </summary>
    /// <param name="latitude">Pickup location latitude</param>
    /// <param name="longitude">Pickup location longitude</param>
    /// <param name="radiusKm">Search radius in kilometers</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if own fleet is available</returns>
    Task<bool> IsOwnFleetAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct = default);

    /// <summary>
    /// Gets detailed rider information by ID.
    /// </summary>
    /// <param name="riderId">Rider ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rider details or null if not found</returns>
    Task<RiderDetails?> GetRiderByIdAsync(
        Guid riderId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets rider details for display to customer (limited info).
    /// Used for tracking page.
    /// </summary>
    /// <param name="riderId">Rider ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Public rider info for customer display</returns>
    Task<RiderPublicInfo?> GetRiderPublicInfoAsync(
        Guid riderId,
        CancellationToken ct = default);

    /// <summary>
    /// Diagnoses why a specific rider would or would not receive a delivery offer
    /// for the given pickup location. Runs every gate used by
    /// <see cref="GetAvailableRidersAsync"/> and reports each one's pass/fail with
    /// the rider's actual value. Ops/debug only.
    /// </summary>
    Task<RiderEligibilityReport> DiagnoseEligibilityAsync(
        Guid riderId,
        double pickupLatitude,
        double pickupLongitude,
        double radiusKm,
        CancellationToken ct = default);

    /// <summary>
    /// Runs the eligibility diagnostic across all riders (capped) for a pickup
    /// location, so callers can see at a glance which riders are eligible and why
    /// the rest are excluded. Ops/debug only.
    /// </summary>
    Task<IReadOnlyList<RiderEligibilityReport>> DiagnoseAllRidersAsync(
        double pickupLatitude,
        double pickupLongitude,
        double radiusKm,
        int maxRiders = 200,
        CancellationToken ct = default);
}

/// <summary>
/// Limited rider information safe to show to customers.
/// </summary>
public sealed record RiderPublicInfo
{
    public required Guid RiderId { get; init; }

    /// <summary>
    /// First name only or partial name for privacy.
    /// Example: "Rahul K."
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Masked phone number.
    /// Example: "+91 98XXX XXXXX"
    /// </summary>
    public required string MaskedPhone { get; init; }

    public required string VehicleType { get; init; }
}