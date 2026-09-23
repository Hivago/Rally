using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Abstractions.Reviews;

namespace RallyAPI.Infrastructure.GoogleMaps;

/// <summary>
/// Legacy Places API (Place Details) reviews lookup. Proxies server-side so the
/// API key never reaches the browser. Google caps this at 5 reviews per place,
/// chosen by their relevance ranking, refreshed roughly weekly on their end —
/// callers are expected to cache aggressively on top of this.
/// </summary>
public sealed class GoogleReviewsService : IGoogleReviewsService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleReviewsService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GoogleReviewsService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleReviewsService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlaceReviewsResult?> GetPlaceReviewsAsync(string placeId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        try
        {
            var url = "https://maps.googleapis.com/maps/api/place/details/json" +
                      $"?place_id={Uri.EscapeDataString(placeId)}" +
                      $"&key={_options.ApiKey}" +
                      "&fields=rating,user_ratings_total,reviews" +
                      "&language=en";

            var response = await _httpClient.GetFromJsonAsync<PlaceDetailApiResponse>(url, JsonOptions, ct);

            if (response?.Status != "OK" || response.Result is null)
            {
                _logger.LogWarning("Place reviews failed for placeId {PlaceId}: status={Status}", placeId, response?.Status);
                return null;
            }

            var r = response.Result;
            var reviews = (r.Reviews ?? [])
                .Select(rv => new ReviewSnippet(
                    rv.AuthorName,
                    rv.ProfilePhotoUrl,
                    rv.Rating,
                    rv.Text ?? string.Empty,
                    rv.RelativeTimeDescription,
                    rv.Time))
                .ToList();

            return new PlaceReviewsResult(r.Rating, r.UserRatingsTotal, reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Place reviews error for placeId: {PlaceId}", placeId);
            return null;
        }
    }

    // ── Google API response DTOs ────────────────────────────

    private sealed class PlaceDetailApiResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public PlaceDetailResult? Result { get; set; }
    }

    private sealed class PlaceDetailResult
    {
        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("user_ratings_total")]
        public int? UserRatingsTotal { get; set; }

        [JsonPropertyName("reviews")]
        public List<GoogleReview>? Reviews { get; set; }
    }

    private sealed class GoogleReview
    {
        [JsonPropertyName("author_name")]
        public string AuthorName { get; set; } = string.Empty;

        [JsonPropertyName("profile_photo_url")]
        public string? ProfilePhotoUrl { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("relative_time_description")]
        public string RelativeTimeDescription { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public long Time { get; set; }
    }
}
