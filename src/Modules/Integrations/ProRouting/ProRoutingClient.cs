using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Integrations.ProRouting.Models;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Integrations.ProRouting;

/// <summary>
/// ProRouting API client implementing IDeliveryQuoteProvider.
/// Acts as an Anti-Corruption Layer between our domain and ProRouting's API.
/// </summary>
public sealed class ProRoutingClient : IDeliveryQuoteProvider
{
    private const string EstimateEndpoint = "partner/estimate";

    private readonly HttpClient _httpClient;
    private readonly ProRoutingOptions _options;
    private readonly ILogger<ProRoutingClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ProviderName => "ProRouting";

    public ProRoutingClient(
        HttpClient httpClient,
        IOptions<ProRoutingOptions> options,
        ILogger<ProRoutingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeliveryQuoteResult> GetQuoteAsync(
        DeliveryQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("ProRouting integration is disabled");
            return DeliveryQuoteResult.Failure("ProRouting integration is disabled", ProviderName);
        }

        try
        {
            var providerRequest = MapToProviderRequest(request);

            // Manual JSON to avoid encoding issues
            var requestJson = System.Text.Json.JsonSerializer.Serialize(providerRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });



            // 🔍 DEBUG: Log the full URL and request
           // var requestJson = System.Text.Json.JsonSerializer.Serialize(providerRequest, JsonOptions);
            _logger.LogInformation(
                "ProRouting Request - URL: {Url}, Body: {Body}",
                $"{_httpClient.BaseAddress}{EstimateEndpoint}",
                requestJson);

            _logger.LogDebug(
                "Requesting quote from ProRouting. City: {City}, OrderAmount: {OrderAmount}",
                request.City,
                request.OrderAmount);

            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");


            var response = await _httpClient.PostAsJsonAsync(
                EstimateEndpoint,
                providerRequest,
                JsonOptions,
                cancellationToken);

            // 🔍 DEBUG: Log response status
            _logger.LogInformation(
                "ProRouting Response - Status: {StatusCode}",
                response.StatusCode);


            return await HandleResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling ProRouting API");
            return DeliveryQuoteResult.Failure($"Network error: {ex.Message}", ProviderName);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout calling ProRouting API");
            return DeliveryQuoteResult.Failure("Request timeout", ProviderName);
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled by the caller, rethrow
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse ProRouting API response");
            return DeliveryQuoteResult.Failure("Invalid response format", ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling ProRouting API");
            return DeliveryQuoteResult.Failure("Unexpected error occurred", ProviderName);
        }
    }

    private async Task<DeliveryQuoteResult> HandleResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ProRouting API returned {StatusCode}: {Content}",
                response.StatusCode,
                content);

            return DeliveryQuoteResult.Failure(
                $"API returned {(int)response.StatusCode}: {response.ReasonPhrase}",
                ProviderName);
        }

        var providerResponse = JsonSerializer.Deserialize<ProRoutingEstimateResponse>(
            content,
            JsonOptions);

        if (providerResponse is null)
        {
            _logger.LogWarning("ProRouting API returned null/empty response");
            return DeliveryQuoteResult.Failure("Empty response from provider", ProviderName);
        }

        return MapToResult(providerResponse);
    }

    /// <summary>
    /// Maps our domain request to ProRouting's API format.
    /// Anti-Corruption Layer: keeps their API structure contained.
    /// </summary>
    private ProRoutingEstimateRequest MapToProviderRequest(DeliveryQuoteRequest request)
    {
        return new ProRoutingEstimateRequest
        {
            Pickup = new ProRoutingLocation
            {
                Lat = request.PickupLatitude,
                Lng = request.PickupLongitude,
                Pincode = request.PickupPincode
            },
            Drop = new ProRoutingLocation
            {
                Lat = request.DropLatitude,
                Lng = request.DropLongitude,
                Pincode = request.DropPincode
            },
            City = request.City,
            OrderCategory = _options.DefaultOrderCategory,
            SearchCategory = _options.DefaultSearchCategory,
            OrderAmount = request.OrderAmount,
            OrderWeight = request.OrderWeight ?? _options.DefaultOrderWeight
        };
    }

    /// <summary>
    /// Maps ProRouting's response to our domain result.
    /// Anti-Corruption Layer: translates their format to ours.
    /// </summary>
    private DeliveryQuoteResult MapToResult(ProRoutingEstimateResponse response)
    {
        if (!response.IsSuccess)
        {
            var errorMessage = !string.IsNullOrEmpty(response.Message)
                ? response.Message
                : $"ProRouting returned status {response.Status}";

            _logger.LogWarning("ProRouting quote failed: {Error}", errorMessage);
            return DeliveryQuoteResult.Failure(errorMessage, ProviderName);
        }

        _logger.LogInformation(
            "ProRouting quote successful. QuoteId: {QuoteId}, Price: {Price}, ETA: {ETA} mins",
            response.EstimateId,
            response.Price,
            response.EstimatedDeliveryTime);

        return DeliveryQuoteResult.Success(
            quoteId: response.EstimateId!,
            price: response.Price!.Value,
            estimatedMinutes: response.EstimatedDeliveryTime!.Value,
            providerName: ProviderName);
    }
}