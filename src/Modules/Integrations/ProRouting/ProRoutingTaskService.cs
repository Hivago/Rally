using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Integrations.ProRouting.Models;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Integrations.ProRouting;

/// <summary>
/// ProRouting service for quotes and task management.
/// Implements IThirdPartyDeliveryProvider.
/// </summary>
public sealed class ProRoutingTaskService : IThirdPartyDeliveryProvider
{
    private const string QuotesEndpoint = "partner/quotes";
    private const string CreateEndpoint = "partner/order/createasync";
    private const string CancelEndpoint = "partner/order/cancel";
    private const string StatusEndpoint = "partner/order/status";

    private readonly HttpClient _httpClient;
    private readonly ProRoutingOptions _options;
    private readonly ILogger<ProRoutingTaskService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public string ProviderName => "ProRouting";

    public ProRoutingTaskService(
        HttpClient httpClient,
        IOptions<ProRoutingOptions> options,
        ILogger<ProRoutingTaskService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    #region IDeliveryQuoteProvider (existing)

    public async Task<DeliveryQuoteResult> GetQuoteAsync(
        DeliveryQuoteRequest request,
        CancellationToken ct = default)
    {
        var quotesResult = await GetQuotesAsync(request, ct);

        if (!quotesResult.IsSuccess || !quotesResult.Quotes.Any())
        {
            return DeliveryQuoteResult.Failure(
                quotesResult.ErrorMessage ?? "No quotes available",
                ProviderName);
        }

        // Select best quote (cheapest with reasonable SLA)
        var bestQuote = SelectBestQuote(quotesResult.Quotes);

        return DeliveryQuoteResult.Success(
            quoteId: quotesResult.QuoteId!,
            price: bestQuote.PriceForward,
            estimatedMinutes: bestQuote.SlaMins,
            providerName: ProviderName);
    }

    #endregion

    #region IThirdPartyDeliveryProvider

    public async Task<ThirdPartyQuotesResult> GetQuotesAsync(
        DeliveryQuoteRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return ThirdPartyQuotesResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = new ProRoutingQuotesRequest
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
                OrderAmount = request.OrderAmount,
                OrderWeight = request.OrderWeight ?? _options.DefaultOrderWeight
            };

            _logger.LogDebug("Requesting quotes from ProRouting for city: {City}", request.City);

            var response = await _httpClient.PostAsJsonAsync(
                QuotesEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProRouting quotes failed: {Status} - {Content}",
                    response.StatusCode, content);
                return ThirdPartyQuotesResult.Failure($"API error: {response.StatusCode}", ProviderName);
            }

            var quotesResponse = JsonSerializer.Deserialize<ProRoutingQuotesResponse>(content, JsonOptions);

            if (quotesResponse is null || !quotesResponse.IsSuccess)
            {
                return ThirdPartyQuotesResult.Failure(
                    quotesResponse?.ErrorMessage ?? "Invalid response",
                    ProviderName);
            }

            var quotes = quotesResponse.Quotes
                .Select(q => new ThirdPartyLspQuote
                {
                    LspId = q.LspId,
                    ItemId = q.ItemId,
                    LogisticsSeller = q.LogisticsSeller,
                    PriceForward = q.PriceForward,
                    PriceRto = q.PriceRto,
                    SlaMins = q.SlaMins,
                    PickupEtaMins = q.PickupEtaMins
                })
                .ToList();

            // Parse validity
            DateTime validUntil = DateTime.UtcNow.AddMinutes(5);
            if (!string.IsNullOrEmpty(quotesResponse.ValidUntil))
            {
                DateTime.TryParse(quotesResponse.ValidUntil, out validUntil);
            }

            _logger.LogInformation(
                "ProRouting returned {Count} quotes, QuoteId: {QuoteId}",
                quotes.Count, quotesResponse.QuoteId);

            return ThirdPartyQuotesResult.Success(
                quotesResponse.QuoteId!,
                validUntil,
                quotes,
                ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ProRouting quotes");
            return ThirdPartyQuotesResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<CreateTaskResult> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return CreateTaskResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = MapToCreateRequest(request);

            _logger.LogInformation(
                "Creating ProRouting task for order: {OrderId}",
                request.OrderNumber);

            var response = await _httpClient.PostAsJsonAsync(
                CreateEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProRouting create failed: {Status} - {Content}",
                    response.StatusCode, content);
                return CreateTaskResult.Failure($"API error: {response.StatusCode}", ProviderName);
            }

            var createResponse = JsonSerializer.Deserialize<ProRoutingCreateResponse>(content, JsonOptions);

            if (createResponse is null || !createResponse.IsSuccess)
            {
                return CreateTaskResult.Failure(
                    createResponse?.ErrorMessage ?? "Task creation failed",
                    ProviderName);
            }

            _logger.LogInformation(
                "ProRouting task created: {TaskId}, State: {State}",
                createResponse.Order!.Id, createResponse.Order.State);

            return CreateTaskResult.Success(
                taskId: createResponse.Order.Id,
                clientOrderId: createResponse.Order.ClientOrderId,
                state: createResponse.Order.State,
                trackingUrl: null, // Comes via webhook
                providerName: ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ProRouting task");
            return CreateTaskResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<CancelTaskResult> CancelTaskAsync(
        string taskId,
        string reason,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return CancelTaskResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var request = new { order_id = taskId, reason };

            _logger.LogInformation("Cancelling ProRouting task: {TaskId}", taskId);

            var response = await _httpClient.PostAsJsonAsync(
                CancelEndpoint, request, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("ProRouting cancel failed: {Content}", content);
                return CancelTaskResult.Failure($"Cancel failed: {response.StatusCode}", ProviderName);
            }

            _logger.LogInformation("ProRouting task cancelled: {TaskId}", taskId);
            return CancelTaskResult.Success(ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling ProRouting task");
            return CancelTaskResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<TaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return TaskStatusResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var url = $"{StatusEndpoint}?order_id={taskId}";

            var response = await _httpClient.GetFromJsonAsync<ProRoutingWebhookPayload>(url, ct);

            if (response is null)
            {
                return TaskStatusResult.Failure("Empty response", ProviderName);
            }

            return TaskStatusResult.Success(
                taskId: response.OrderId,
                state: response.State,
                riderName: response.Agent?.Name,
                riderPhone: response.Agent?.Phone,
                trackingUrl: response.TrackingUrl,
                providerName: ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ProRouting task status");
            return TaskStatusResult.Failure(ex.Message, ProviderName);
        }
    }

    #endregion

    #region Private Methods

    private ProRoutingCreateRequest MapToCreateRequest(CreateTaskRequest request)
    {
        return new ProRoutingCreateRequest
        {
            ClientOrderId = request.OrderNumber,
            RetailOrderId = request.OrderId.ToString(),
            Pickup = new ProRoutingPickupDetails
            {
                Lat = request.PickupLatitude,
                Lng = request.PickupLongitude,
                Pincode = request.PickupPincode,
                Phone = request.PickupContactPhone,
                Address = new ProRoutingAddress
                {
                    Name = request.PickupContactName,
                    Line1 = request.PickupAddressLine1,
                    Line2 = request.PickupAddressLine2,
                    City = request.PickupCity,
                    State = request.PickupState
                },
                StoreId = request.StoreId
            },
            Drop = new ProRoutingDropDetails
            {
                Lat = request.DropLatitude,
                Lng = request.DropLongitude,
                Pincode = request.DropPincode,
                Phone = request.DropContactPhone,
                Address = new ProRoutingAddress
                {
                    Name = request.DropContactName,
                    Line1 = request.DropAddressLine1,
                    Line2 = request.DropAddressLine2,
                    City = request.DropCity,
                    State = request.DropState
                }
            },
            CallbackUrl = request.CallbackUrl,
            OrderAmount = request.OrderAmount,
            CodAmount = request.CodAmount,
            OrderWeight = request.OrderWeight,
            OrderItems = request.OrderItems
                .Select(i => new ProRoutingOrderItem
                {
                    Name = i.Name,
                    Qty = i.Quantity,
                    Price = i.Price
                })
                .ToList(),
            OrderReady = request.IsOrderReady,
            SelectCriteria = new ProRoutingSelectCriteria
            {
                Mode = request.SelectionMode,
                MaxAmount = request.MaxAmount,
                MaxSla = request.MaxSlaMins,
                LspId = request.SelectedLspId,
                ItemId = request.SelectedItemId
            },
            Note1 = request.Notes
        };
    }

    private ThirdPartyLspQuote SelectBestQuote(IReadOnlyList<ThirdPartyLspQuote> quotes)
    {
        // Filter: reasonable price (> ₹5 to exclude test), SLA < 90 mins
        var validQuotes = quotes
            .Where(q => q.PriceForward >= 5)
            .Where(q => q.SlaMins <= 90)
            .Where(q => q.PickupEtaMins <= 30)
            .ToList();

        if (!validQuotes.Any())
        {
            validQuotes = quotes.ToList();
        }

        // Sort by price, then by SLA
        return validQuotes
            .OrderBy(q => q.PriceForward)
            .ThenBy(q => q.SlaMins)
            .First();
    }

    #endregion
}