using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Infrastructure.GoogleMaps.Models;
using RallyAPI.SharedKernel.Abstractions.Distance;

namespace RallyAPI.Infrastructure.GoogleMaps;

/// <summary>
/// Google Maps Distance Matrix API implementation.
/// </summary>
public sealed class GoogleMapsDistanceCalculator : IDistanceCalculator
{
    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleMapsDistanceCalculator> _logger;

    public GoogleMapsDistanceCalculator(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleMapsDistanceCalculator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DistanceResult> GetDistanceAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Google Maps API is disabled, using Haversine fallback");
            return GetHaversineFallback(originLat, originLng, destinationLat, destinationLng);
        }

        try
        {
            var origin = $"{originLat},{originLng}";
            var destination = $"{destinationLat},{destinationLng}";

            var url = $"{_options.BaseUrl}/distancematrix/json" +
                      $"?origins={origin}" +
                      $"&destinations={destination}" +
                      $"&mode={_options.TravelMode}" +
                      $"&region={_options.Region}" +
                      $"&key={_options.ApiKey}";

            _logger.LogDebug(
                "Requesting distance: ({OriginLat}, {OriginLng}) → ({DestLat}, {DestLng})",
                originLat, originLng, destinationLat, destinationLng);

            var response = await _httpClient.GetFromJsonAsync<DistanceMatrixResponse>(url, ct);

            if (response is null)
            {
                _logger.LogWarning("Google Maps returned null response");
                return DistanceResult.Failure("Empty response from Google Maps");
            }

            if (response.Status != "OK")
            {
                _logger.LogWarning(
                    "Google Maps API error: {Status} - {Message}",
                    response.Status, response.ErrorMessage);
                return DistanceResult.Failure(response.ErrorMessage ?? response.Status);
            }

            var element = response.Rows.FirstOrDefault()?.Elements.FirstOrDefault();

            if (element is null || element.Status != "OK")
            {
                _logger.LogWarning("No route found: {Status}", element?.Status);
                return DistanceResult.Failure(element?.Status ?? "No route found");
            }

            _logger.LogInformation(
                "Distance calculated: {Distance}, {Duration}",
                element.Distance?.Text, element.Duration?.Text);

            return DistanceResult.Success(
                distanceMeters: element.Distance!.Value,
                durationSeconds: element.Duration!.Value,
                distanceText: element.Distance.Text,
                durationText: element.Duration.Text);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Google Maps API");
            return GetHaversineFallback(originLat, originLng, destinationLat, destinationLng);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout calling Google Maps API");
            return GetHaversineFallback(originLat, originLng, destinationLat, destinationLng);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Google Maps API");
            return GetHaversineFallback(originLat, originLng, destinationLat, destinationLng);
        }
    }

    public async Task<IReadOnlyList<DistanceResult>> GetDistancesAsync(
        double originLat,
        double originLng,
        IReadOnlyList<(double lat, double lng)> destinations,
        CancellationToken ct = default)
    {
        if (!destinations.Any())
            return [];

        if (!_options.Enabled)
        {
            return destinations
                .Select(d => GetHaversineFallback(originLat, originLng, d.lat, d.lng))
                .ToList();
        }

        try
        {
            var origin = $"{originLat},{originLng}";
            var destinationsStr = string.Join("|", destinations.Select(d => $"{d.lat},{d.lng}"));

            var url = $"{_options.BaseUrl}/distancematrix/json" +
                      $"?origins={origin}" +
                      $"&destinations={destinationsStr}" +
                      $"&mode={_options.TravelMode}" +
                      $"&region={_options.Region}" +
                      $"&key={_options.ApiKey}";

            _logger.LogDebug(
                "Requesting distances to {Count} destinations",
                destinations.Count);

            var response = await _httpClient.GetFromJsonAsync<DistanceMatrixResponse>(url, ct);

            if (response?.Status != "OK" || !response.Rows.Any())
            {
                _logger.LogWarning("Google Maps batch request failed, using fallback");
                return destinations
                    .Select(d => GetHaversineFallback(originLat, originLng, d.lat, d.lng))
                    .ToList();
            }

            var results = new List<DistanceResult>();
            var elements = response.Rows[0].Elements;

            for (int i = 0; i < destinations.Count; i++)
            {
                if (i < elements.Count && elements[i].Status == "OK")
                {
                    results.Add(DistanceResult.Success(
                        distanceMeters: elements[i].Distance!.Value,
                        durationSeconds: elements[i].Duration!.Value,
                        distanceText: elements[i].Distance.Text,
                        durationText: elements[i].Duration.Text));
                }
                else
                {
                    results.Add(GetHaversineFallback(
                        originLat, originLng,
                        destinations[i].lat, destinations[i].lng));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch distance calculation");
            return destinations
                .Select(d => GetHaversineFallback(originLat, originLng, d.lat, d.lng))
                .ToList();
        }
    }

    /// <summary>
    /// Fallback to Haversine with 1.4x multiplier for road estimate.
    /// </summary>
    private static DistanceResult GetHaversineFallback(
        double lat1, double lng1,
        double lat2, double lng2)
    {
        var straightLine = CalculateHaversine(lat1, lng1, lat2, lng2);
        var estimatedRoad = straightLine * 1.4; // Road distance estimate
        var estimatedMinutes = (int)Math.Ceiling(estimatedRoad / 20 * 60); // 20 km/h average

        return DistanceResult.Success(
            distanceMeters: (int)(estimatedRoad * 1000),
            durationSeconds: estimatedMinutes * 60,
            distanceText: $"{estimatedRoad:F1} km (est.)",
            durationText: $"{estimatedMinutes} mins (est.)");
    }

    private static double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}   