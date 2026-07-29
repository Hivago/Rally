using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Abstractions.Geocoding;

namespace RallyAPI.Infrastructure.GoogleMaps;

/// <summary>
/// Places API (New) implementation of <see cref="IGeocodingService"/>.
/// Autocomplete + place details hit https://places.googleapis.com/v1 (auth via the
/// X-Goog-Api-Key header, place details via an X-Goog-FieldMask). Reverse geocoding is
/// unchanged — it still uses the Geocoding API, identical to the legacy service.
/// Selected over <see cref="GoogleGeocodingService"/> when GoogleMaps:UsePlacesApiNew is true.
/// </summary>
public sealed class GooglePlacesV1Service : IGeocodingService
{
    private const string PlacesBaseUrl = "https://places.googleapis.com/v1";

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GooglePlacesV1Service> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GooglePlacesV1Service(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        ILogger<GooglePlacesV1Service> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    // ── Autocomplete — Places API (New): POST /places:autocomplete ──────────

    public async Task<IReadOnlyList<PlaceSuggestion>> AutocompleteAsync(
        string input,
        double? sessionLatitude = null,
        double? sessionLongitude = null,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return [];

        try
        {
            var body = new AutocompleteRequest
            {
                Input = input,
                IncludedRegionCodes = [_options.Region],
                LanguageCode = "en",
                LocationBias = (sessionLatitude.HasValue && sessionLongitude.HasValue)
                    ? new LocationBias
                    {
                        Circle = new Circle
                        {
                            Center = new LatLng { Latitude = sessionLatitude.Value, Longitude = sessionLongitude.Value },
                            Radius = 50000
                        }
                    }
                    : null
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{PlacesBaseUrl}/places:autocomplete")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Places(New) autocomplete failed: {Status} {Body}", (int)response.StatusCode, error);
                return [];
            }

            var parsed = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(JsonOptions, ct);
            if (parsed?.Suggestions is null)
                return [];

            return parsed.Suggestions
                .Where(s => s.PlacePrediction is not null)
                .Take(maxResults)
                .Select(s =>
                {
                    var p = s.PlacePrediction!;
                    var description = p.Text?.Text ?? string.Empty;
                    return new PlaceSuggestion(
                        p.PlaceId,
                        description,
                        p.StructuredFormat?.MainText?.Text ?? description,
                        p.StructuredFormat?.SecondaryText?.Text ?? string.Empty);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Places(New) autocomplete error for input: {Input}", input);
            return [];
        }
    }

    // ── Place details — Places API (New): GET /places/{id} + FieldMask ──────

    public async Task<PlaceDetail?> GetPlaceDetailAsync(string placeId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{PlacesBaseUrl}/places/{Uri.EscapeDataString(placeId)}?languageCode=en");
            request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
            // FieldMask is REQUIRED by the new API and controls billing — request only what we map.
            request.Headers.Add("X-Goog-FieldMask", "id,formattedAddress,location,addressComponents");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Places(New) details failed: {Status} {Body}", (int)response.StatusCode, error);
                return null;
            }

            var place = await response.Content.ReadFromJsonAsync<PlaceV1>(JsonOptions, ct);
            if (place is null)
                return null;

            return new PlaceDetail(
                place.Id,
                place.FormattedAddress ?? string.Empty,
                place.Location?.Latitude ?? 0,
                place.Location?.Longitude ?? 0,
                ExtractComponent(place.AddressComponents, "locality"),
                ExtractComponent(place.AddressComponents, "postal_code"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Places(New) details error for placeId: {PlaceId}", placeId);
            return null;
        }
    }

    // ── Reverse geocode — Geocoding API (unchanged from the legacy service) ──

    public async Task<ReverseGeocodeResult> ReverseGeocodeAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return ReverseGeocodeResult.Failure("Google Maps API is disabled");

        try
        {
            var url = $"https://maps.googleapis.com/maps/api/geocode/json" +
                      $"?latlng={latitude},{longitude}" +
                      $"&key={_options.ApiKey}" +
                      $"&region={_options.Region}" +
                      $"&language=en";

            var response = await _httpClient.GetFromJsonAsync<GeocodeApiResponse>(url, JsonOptions, ct);

            if (response?.Status != "OK" || response.Results is not { Count: > 0 })
            {
                _logger.LogWarning("Reverse geocode failed: status={Status}", response?.Status);
                return ReverseGeocodeResult.Failure(response?.Status ?? "Empty response");
            }

            var best = response.Results[0];
            return ReverseGeocodeResult.Success(
                best.FormattedAddress,
                best.PlaceId,
                ExtractGeocodeComponent(best, "locality"),
                ExtractGeocodeComponent(best, "postal_code"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reverse geocode error for ({Lat}, {Lng})", latitude, longitude);
            return ReverseGeocodeResult.Failure("Geocoding service unavailable");
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    private static string? ExtractComponent(List<AddressComponentV1>? components, string type)
        => components?.FirstOrDefault(c => c.Types is not null && c.Types.Contains(type))?.LongText;

    private static string? ExtractGeocodeComponent(GeocodeResult result, string type)
        => result.AddressComponents?.FirstOrDefault(c => c.Types.Contains(type))?.LongName;

    // ── Places API (New) DTOs — camelCase via JsonOptions policy ────────────

    private sealed class AutocompleteRequest
    {
        public string Input { get; set; } = string.Empty;
        public string[] IncludedRegionCodes { get; set; } = [];
        public string LanguageCode { get; set; } = "en";
        public LocationBias? LocationBias { get; set; }
    }

    private sealed class LocationBias
    {
        public Circle Circle { get; set; } = new();
    }

    private sealed class Circle
    {
        public LatLng Center { get; set; } = new();
        public int Radius { get; set; }
    }

    private sealed class LatLng
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class AutocompleteResponse
    {
        public List<Suggestion>? Suggestions { get; set; }
    }

    private sealed class Suggestion
    {
        public PlacePrediction? PlacePrediction { get; set; }
    }

    private sealed class PlacePrediction
    {
        public string PlaceId { get; set; } = string.Empty;
        public TextValue? Text { get; set; }
        public StructuredFormat? StructuredFormat { get; set; }
    }

    private sealed class StructuredFormat
    {
        public TextValue? MainText { get; set; }
        public TextValue? SecondaryText { get; set; }
    }

    private sealed class TextValue
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class PlaceV1
    {
        public string Id { get; set; } = string.Empty;
        public string? FormattedAddress { get; set; }
        public LocationV1? Location { get; set; }
        public List<AddressComponentV1>? AddressComponents { get; set; }
    }

    private sealed class LocationV1
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class AddressComponentV1
    {
        public string LongText { get; set; } = string.Empty;
        public string ShortText { get; set; } = string.Empty;
        public List<string>? Types { get; set; }
    }

    // ── Geocoding API DTOs — snake_case via explicit names ──────────────────

    private sealed class GeocodeApiResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<GeocodeResult> Results { get; set; } = new();
    }

    private sealed class GeocodeResult
    {
        [JsonPropertyName("formatted_address")]
        public string FormattedAddress { get; set; } = string.Empty;

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; } = string.Empty;

        [JsonPropertyName("address_components")]
        public List<GeocodeAddressComponent>? AddressComponents { get; set; }
    }

    private sealed class GeocodeAddressComponent
    {
        [JsonPropertyName("long_name")]
        public string LongName { get; set; } = string.Empty;

        [JsonPropertyName("types")]
        public List<string> Types { get; set; } = new();
    }
}
