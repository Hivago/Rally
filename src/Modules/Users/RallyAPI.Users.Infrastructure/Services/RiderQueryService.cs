using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.Utilities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Infrastructure.Persistence;

namespace RallyAPI.Users.Infrastructure.Services;

/// <summary>
/// Implementation of IRiderQueryService.
/// Queries rider data for the Delivery module.
/// </summary>
public sealed class RiderQueryService : IRiderQueryService
{
    private readonly UsersDbContext _dbContext;
    private readonly ILogger<RiderQueryService> _logger;

    /// <summary>
    /// Maximum age of location data to consider "fresh" (in minutes).
    /// </summary>
    private const int MaxLocationAgeMinutes = 10;

    public RiderQueryService(
        UsersDbContext dbContext,
        ILogger<RiderQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AvailableRider>> GetAvailableRidersAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Finding available riders within {RadiusKm}km of ({Lat}, {Lng})",
            radiusKm, latitude, longitude);

        // Get bounding box for initial filter (faster than calculating distance for all riders)
        var boundingBox = GeoCalculator.GetBoundingBox(latitude, longitude, radiusKm);
        var locationCutoff = DateTime.UtcNow.AddMinutes(-MaxLocationAgeMinutes);

        // Query riders with basic filters
        var candidateRiders = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.IsOnline)
            .Where(r => r.KycStatus == KycStatus.Verified)
            .Where(r => r.CurrentDeliveryId == null)
            .Where(r => r.CurrentLatitude.HasValue && r.CurrentLongitude.HasValue)
            .Where(r => r.LastLocationUpdate.HasValue && r.LastLocationUpdate.Value >= locationCutoff)
            // Bounding box filter (rough filter, faster)
            .Where(r => (double)r.CurrentLatitude!.Value >= boundingBox.MinLatitude)
            .Where(r => (double)r.CurrentLatitude!.Value <= boundingBox.MaxLatitude)
            .Where(r => (double)r.CurrentLongitude!.Value >= boundingBox.MinLongitude)
            .Where(r => (double)r.CurrentLongitude!.Value <= boundingBox.MaxLongitude)
            .Select(r => new
            {
                r.Id,
                r.Name,
                Phone = r.Phone.Value, // Assuming PhoneNumber has Value property
                Latitude = (double)r.CurrentLatitude!.Value,
                Longitude = (double)r.CurrentLongitude!.Value,
                VehicleType = r.VehicleType.ToString(),
                r.LastLocationUpdate
            })
            .ToListAsync(ct);

        _logger.LogDebug(
            "Found {Count} candidate riders in bounding box",
            candidateRiders.Count);

        // Calculate actual distance and filter by radius
        var availableRiders = candidateRiders
            .Select(r => new
            {
                Rider = r,
                Distance = GeoCalculator.CalculateDistanceKm(
                    latitude, longitude,
                    r.Latitude, r.Longitude)
            })
            .Where(x => x.Distance <= radiusKm)
            .OrderBy(x => x.Distance)
            .Take(maxResults)
            .Select(x => new AvailableRider
            {
                RiderId = x.Rider.Id,
                Name = x.Rider.Name,
                Phone = x.Rider.Phone,
                Latitude = x.Rider.Latitude,
                Longitude = x.Rider.Longitude,
                DistanceToPickupKm = Math.Round(x.Distance, 2),
                VehicleType = x.Rider.VehicleType,
                LocationUpdatedAt = x.Rider.LastLocationUpdate!.Value
            })
            .ToList();

        _logger.LogInformation(
            "Found {Count} available riders within {RadiusKm}km",
            availableRiders.Count, radiusKm);

        return availableRiders;
    }

    /// <inheritdoc />
    public async Task<bool> IsOwnFleetAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Checking own fleet availability within {RadiusKm}km of ({Lat}, {Lng})",
            radiusKm, latitude, longitude);

        var boundingBox = GeoCalculator.GetBoundingBox(latitude, longitude, radiusKm);
        var locationCutoff = DateTime.UtcNow.AddMinutes(-MaxLocationAgeMinutes);

        // Check if at least one rider exists in the area
        var hasAvailableRider = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.IsOnline)
            .Where(r => r.KycStatus == KycStatus.Verified)
            .Where(r => r.CurrentDeliveryId == null)
            .Where(r => r.CurrentLatitude.HasValue && r.CurrentLongitude.HasValue)
            .Where(r => r.LastLocationUpdate.HasValue && r.LastLocationUpdate.Value >= locationCutoff)
            .Where(r => (double)r.CurrentLatitude!.Value >= boundingBox.MinLatitude)
            .Where(r => (double)r.CurrentLatitude!.Value <= boundingBox.MaxLatitude)
            .Where(r => (double)r.CurrentLongitude!.Value >= boundingBox.MinLongitude)
            .Where(r => (double)r.CurrentLongitude!.Value <= boundingBox.MaxLongitude)
            .AnyAsync(ct);

        _logger.LogInformation(
            "Own fleet availability check: {Available}",
            hasAvailableRider);

        return hasAvailableRider;
    }

    /// <inheritdoc />
    public async Task<RiderDetails?> GetRiderByIdAsync(
        Guid riderId,
        CancellationToken ct = default)
    {
        var rider = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.Id == riderId)
            .Select(r => new RiderDetails
            {
                RiderId = r.Id,
                Name = r.Name,
                Phone = r.Phone.Value,
                VehicleNumber = r.VehicleNumber,
                VehicleType = r.VehicleType.ToString(),
                IsOnline = r.IsOnline,
                IsActive = r.IsActive,
                CurrentDeliveryId = r.CurrentDeliveryId,
                CurrentLatitude = r.CurrentLatitude.HasValue ? (double)r.CurrentLatitude.Value : null,
                CurrentLongitude = r.CurrentLongitude.HasValue ? (double)r.CurrentLongitude.Value : null,
                LastLocationUpdate = r.LastLocationUpdate
            })
            .FirstOrDefaultAsync(ct);

        return rider;
    }

    /// <inheritdoc />
    public async Task<RiderPublicInfo?> GetRiderPublicInfoAsync(
        Guid riderId,
        CancellationToken ct = default)
    {
        var rider = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.Id == riderId)
            .Select(r => new
            {
                r.Id,
                r.Name,
                Phone = r.Phone.Value,
                VehicleType = r.VehicleType.ToString()
            })
            .FirstOrDefaultAsync(ct);

        if (rider is null)
            return null;

        return new RiderPublicInfo
        {
            RiderId = rider.Id,
            DisplayName = GetDisplayName(rider.Name),
            MaskedPhone = MaskPhoneNumber(rider.Phone),
            VehicleType = rider.VehicleType
        };
    }

    /// <inheritdoc />
    public async Task<RiderEligibilityReport> DiagnoseEligibilityAsync(
        Guid riderId,
        double pickupLatitude,
        double pickupLongitude,
        double radiusKm,
        CancellationToken ct = default)
    {
        var rider = await _dbContext.Riders
            .AsNoTracking()
            .Where(r => r.Id == riderId)
            .Select(r => new RiderEligibilitySnapshot
            {
                Id = r.Id,
                Name = r.Name,
                IsActive = r.IsActive,
                IsOnline = r.IsOnline,
                KycStatus = r.KycStatus.ToString(),
                CurrentDeliveryId = r.CurrentDeliveryId,
                Latitude = r.CurrentLatitude.HasValue ? (double)r.CurrentLatitude.Value : null,
                Longitude = r.CurrentLongitude.HasValue ? (double)r.CurrentLongitude.Value : null,
                LastLocationUpdate = r.LastLocationUpdate
            })
            .FirstOrDefaultAsync(ct);

        if (rider is null)
        {
            return new RiderEligibilityReport
            {
                RiderId = riderId,
                Found = false,
                Eligible = false,
                Checks = [],
                FailedChecks = ["Exists"]
            };
        }

        return BuildReport(rider, pickupLatitude, pickupLongitude, radiusKm);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RiderEligibilityReport>> DiagnoseAllRidersAsync(
        double pickupLatitude,
        double pickupLongitude,
        double radiusKm,
        int maxRiders = 200,
        CancellationToken ct = default)
    {
        // Intentionally NOT pre-filtered by location/online — the whole point of the
        // diagnostic is to surface WHY a rider is excluded (e.g. a far-away or offline
        // rider), so we evaluate every rider and let the gates explain themselves.
        var riders = await _dbContext.Riders
            .AsNoTracking()
            .OrderByDescending(r => r.IsOnline)
            .ThenBy(r => r.Name)
            .Take(maxRiders)
            .Select(r => new RiderEligibilitySnapshot
            {
                Id = r.Id,
                Name = r.Name,
                IsActive = r.IsActive,
                IsOnline = r.IsOnline,
                KycStatus = r.KycStatus.ToString(),
                CurrentDeliveryId = r.CurrentDeliveryId,
                Latitude = r.CurrentLatitude.HasValue ? (double)r.CurrentLatitude.Value : null,
                Longitude = r.CurrentLongitude.HasValue ? (double)r.CurrentLongitude.Value : null,
                LastLocationUpdate = r.LastLocationUpdate
            })
            .ToListAsync(ct);

        return riders
            .Select(r => BuildReport(r, pickupLatitude, pickupLongitude, radiusKm))
            // Eligible first, then closest to pickup.
            .OrderByDescending(r => r.Eligible)
            .ThenBy(r => r.DistanceToPickupKm ?? double.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Evaluates every offer-eligibility gate for one rider, in the same order
    /// <see cref="GetAvailableRidersAsync"/> applies them. Single source of truth for
    /// both diagnostic entry points.
    /// </summary>
    private static RiderEligibilityReport BuildReport(
        RiderEligibilitySnapshot r,
        double pickupLatitude,
        double pickupLongitude,
        double radiusKm)
    {
        var locationCutoff = DateTime.UtcNow.AddMinutes(-MaxLocationAgeMinutes);
        var hasLocation = r.Latitude.HasValue && r.Longitude.HasValue;

        double? distanceKm = hasLocation
            ? Math.Round(GeoCalculator.CalculateDistanceKm(
                pickupLatitude, pickupLongitude, r.Latitude!.Value, r.Longitude!.Value), 2)
            : null;

        var locationAgeText = r.LastLocationUpdate.HasValue
            ? $"{(DateTime.UtcNow - r.LastLocationUpdate.Value).TotalMinutes:F1} min ago"
            : "never";

        var checks = new List<RiderEligibilityCheck>
        {
            new() { Name = "IsActive", Passed = r.IsActive,
                Actual = r.IsActive.ToString(), Requirement = "true" },
            new() { Name = "IsOnline", Passed = r.IsOnline,
                Actual = r.IsOnline.ToString(), Requirement = "true" },
            new() { Name = "KycVerified", Passed = r.KycStatus == "Verified",
                Actual = r.KycStatus, Requirement = "Verified" },
            new() { Name = "NotOnAnotherDelivery", Passed = r.CurrentDeliveryId is null,
                Actual = r.CurrentDeliveryId?.ToString() ?? "null", Requirement = "null (no active delivery)" },
            new() { Name = "HasLocation", Passed = hasLocation,
                Actual = hasLocation ? $"({r.Latitude}, {r.Longitude})" : "missing",
                Requirement = "lat/lng present" },
            new() { Name = "LocationFresh",
                Passed = r.LastLocationUpdate.HasValue && r.LastLocationUpdate.Value >= locationCutoff,
                Actual = locationAgeText, Requirement = $"within {MaxLocationAgeMinutes} min" },
            new() { Name = "WithinSearchRadius",
                Passed = distanceKm.HasValue && distanceKm.Value <= radiusKm,
                Actual = distanceKm.HasValue ? $"{distanceKm.Value} km" : "unknown (no location)",
                Requirement = $"<= {radiusKm} km from pickup" }
        };

        var failed = checks.Where(c => !c.Passed).Select(c => c.Name).ToList();

        return new RiderEligibilityReport
        {
            RiderId = r.Id,
            RiderName = r.Name,
            Found = true,
            Eligible = failed.Count == 0,
            DistanceToPickupKm = distanceKm,
            Checks = checks,
            FailedChecks = failed
        };
    }

    private sealed class RiderEligibilitySnapshot
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool IsOnline { get; init; }
        public string KycStatus { get; init; } = string.Empty;
        public Guid? CurrentDeliveryId { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public DateTime? LastLocationUpdate { get; init; }
    }

    /// <summary>
    /// Creates a display name (first name + last initial).
    /// "Rahul Kumar" → "Rahul K."
    /// </summary>
    private static string GetDisplayName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Rider";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
            return parts[0];

        return $"{parts[0]} {parts[^1][0]}.";
    }

    /// <summary>
    /// Masks a phone number for privacy.
    /// "9876543210" → "+91 98XXX XX210"
    /// </summary>
    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Not available";

        // Remove any non-digit characters
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length < 10)
            return "Not available";

        // Take last 10 digits
        var last10 = digits.Length > 10 ? digits[^10..] : digits;

        // Format: +91 98XXX XX210
        return $"+91 {last10[..2]}XXX XX{last10[^3..]}";
    }
}