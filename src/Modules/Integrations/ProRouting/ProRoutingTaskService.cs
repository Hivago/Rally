using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Integrations.ProRouting.Models;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Integrations.ProRouting;

/// <summary>
/// ProRouting service for quotes, task management, and IGM (issue/grievance) handling.
/// Implements IThirdPartyDeliveryProvider AND IIgmProvider on the same HttpClient
/// so both share auth, base URL, and timeouts.
/// </summary>
public sealed class ProRoutingTaskService : IThirdPartyDeliveryProvider, IIgmProvider
{
    private const string QuotesEndpoint = "partner/quotes";
    private const string CreateEndpoint = "partner/order/createasync";
    private const string CancelEndpoint = "partner/order/cancel";
    private const string StatusEndpoint = "partner/order/status";
    private const string UpdateEndpoint = "partner/order/update";
    private const string IssueEndpoint = "partner/order/issue";
    private const string IssueStatusEndpoint = "partner/order/issue_status";
    private const string IssueCloseEndpoint = "partner/order/issue_close";

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

            // Parse validity. ProRouting returns timestamps without a TZ suffix, so DateTime.TryParse
            // produces Kind=Unspecified which Postgres rejects on `timestamp with time zone` columns.
            // Force UTC: assume universal, then convert to UTC kind.
            DateTime validUntil = DateTime.UtcNow.AddMinutes(5);
            if (!string.IsNullOrEmpty(quotesResponse.ValidUntil)
                && DateTime.TryParse(
                    quotesResponse.ValidUntil,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                validUntil = parsed;
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
            var request = new { order_id = taskId };

            var response = await _httpClient.PostAsJsonAsync(
                StatusEndpoint, request, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProRouting status failed: {Status} - {Content}",
                    response.StatusCode, content);
                return TaskStatusResult.Failure($"API error: {response.StatusCode}", ProviderName);
            }

            var payload = JsonSerializer.Deserialize<ProRoutingWebhookPayload>(content, JsonOptions);

            if (payload is null)
            {
                return TaskStatusResult.Failure("Empty response", ProviderName);
            }

            return TaskStatusResult.Success(
                taskId: payload.OrderId,
                state: payload.State,
                riderName: payload.Agent?.Name,
                riderPhone: payload.Agent?.Phone,
                trackingUrl: payload.TrackingUrl,
                providerName: ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ProRouting task status");
            return TaskStatusResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<UpdateOrderResult> UpdateOrderAsync(
        UpdateOrderRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return UpdateOrderResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = new ProRoutingUpdateRequest
            {
                Order = new ProRoutingUpdateOrder
                {
                    Id = request.ExternalTaskId,
                    PickupCode = request.PickupCode,
                    DropCode = request.DropCode,
                    CustomerPromisedTime = request.CustomerPromisedTime,
                    OrderReady = request.OrderReady
                }
            };

            _logger.LogInformation(
                "Updating ProRouting task {TaskId} with OTPs (orderReady={OrderReady})",
                request.ExternalTaskId, request.OrderReady);

            var response = await _httpClient.PostAsJsonAsync(
                UpdateEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProRouting update failed: {Status} - {Content}",
                    response.StatusCode, content);
                return UpdateOrderResult.Failure(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var updateResponse = JsonSerializer.Deserialize<ProRoutingUpdateResponse>(content, JsonOptions);

            if (updateResponse is null || !updateResponse.IsSuccess)
            {
                return UpdateOrderResult.Failure(
                    updateResponse?.Message ?? "Update failed", ProviderName);
            }

            _logger.LogInformation(
                "ProRouting task updated. State: {State}",
                updateResponse.Order?.State);

            return UpdateOrderResult.Success(
                updateResponse.Order?.State ?? "Unknown",
                ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ProRouting task {TaskId}", request.ExternalTaskId);
            return UpdateOrderResult.Failure(ex.Message, ProviderName);
        }
    }

    #endregion

    #region IIgmProvider

    public async Task<RaiseIssueResult> RaiseIssueAsync(
        RaiseIssueRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return RaiseIssueResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = new ProRoutingIssueRequest
            {
                Order = new ProRoutingIssueOrderRef { Id = request.ExternalTaskId },
                Issue = new ProRoutingIssueCategory
                {
                    Category = request.Category,
                    SubCategory = request.SubCategory
                },
                Description = new ProRoutingIssueDescription
                {
                    Line1 = request.DescriptionShort,
                    Line2 = request.DescriptionLong
                }
            };

            _logger.LogInformation(
                "Raising ProRouting issue for task {TaskId} ({Category}/{SubCategory})",
                request.ExternalTaskId, request.Category, request.SubCategory);

            var response = await _httpClient.PostAsJsonAsync(
                IssueEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProRouting issue raise failed: {Status} - {Content}",
                    response.StatusCode, content);
                return RaiseIssueResult.Failure(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var issueResponse = JsonSerializer.Deserialize<ProRoutingIssueResponse>(content, JsonOptions);

            if (issueResponse is null || !issueResponse.IsSuccess)
            {
                return RaiseIssueResult.Failure(
                    issueResponse?.Message ?? "Issue raise failed", ProviderName);
            }

            _logger.LogInformation(
                "ProRouting issue raised: {IssueId} ({State})",
                issueResponse.Issue!.Id, issueResponse.Issue.State);

            return RaiseIssueResult.Success(
                issueResponse.Issue.Id!,
                issueResponse.Issue.State ?? "Processing",
                ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising ProRouting issue for task {TaskId}", request.ExternalTaskId);
            return RaiseIssueResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<IssueStatusResult> GetIssueStatusAsync(
        string externalIssueId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return IssueStatusResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = new ProRoutingIssueStatusRequest
            {
                Issue = new ProRoutingIssueOrderRef { Id = externalIssueId }
            };

            var response = await _httpClient.PostAsJsonAsync(
                IssueStatusEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProRouting issue_status failed: {Status} - {Content}",
                    response.StatusCode, content);
                return IssueStatusResult.Failure(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var statusResponse = JsonSerializer.Deserialize<ProRoutingIssueResponse>(content, JsonOptions);

            if (statusResponse is null || !statusResponse.IsSuccess)
            {
                return IssueStatusResult.Failure(
                    statusResponse?.Message ?? "Issue status fetch failed", ProviderName);
            }

            return IssueStatusResult.Success(
                statusResponse.Issue!.Id!,
                statusResponse.Issue.State ?? "Unknown",
                statusResponse.Issue.Resolution?.ShortDesc,
                statusResponse.Issue.Resolution?.LongDesc,
                statusResponse.Issue.Resolution?.ActionTriggered,
                statusResponse.Issue.Resolution?.RefundAmount,
                ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ProRouting issue status {IssueId}", externalIssueId);
            return IssueStatusResult.Failure(ex.Message, ProviderName);
        }
    }

    public async Task<CloseIssueResult> CloseIssueAsync(
        CloseIssueRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return CloseIssueResult.Failure("ProRouting is disabled", ProviderName);
        }

        try
        {
            var providerRequest = new ProRoutingIssueCloseRequest
            {
                Issue = new ProRoutingIssueCloseDetails
                {
                    Id = request.ExternalIssueId,
                    Rating = request.Rating,
                    RefundByLsp = request.RefundByLsp,
                    RefundToClient = request.RefundToClient
                }
            };

            _logger.LogInformation(
                "Closing ProRouting issue {IssueId} (rating={Rating})",
                request.ExternalIssueId, request.Rating);

            var response = await _httpClient.PostAsJsonAsync(
                IssueCloseEndpoint, providerRequest, JsonOptions, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProRouting issue_close failed: {Status} - {Content}",
                    response.StatusCode, content);
                return CloseIssueResult.Failure(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var closeResponse = JsonSerializer.Deserialize<ProRoutingIssueCloseResponse>(content, JsonOptions);

            if (closeResponse is null || !closeResponse.IsSuccess)
            {
                return CloseIssueResult.Failure(
                    closeResponse?.Message ?? "Issue close failed", ProviderName);
            }

            return CloseIssueResult.Success(ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing ProRouting issue {IssueId}", request.ExternalIssueId);
            return CloseIssueResult.Failure(ex.Message, ProviderName);
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
                Phone = NormalizeIndianPhone(request.PickupContactPhone),
                Address = new ProRoutingAddress
                {
                    Name = request.PickupContactName,
                    Line1 = request.PickupAddressLine1,
                    Line2 = request.PickupAddressLine2,
                    City = request.PickupCity,
                    State = request.PickupState
                },
                StoreId = request.StoreId,
                Otp = request.PickupCode
            },
            Drop = new ProRoutingDropDetails
            {
                Lat = request.DropLatitude,
                Lng = request.DropLongitude,
                Pincode = request.DropPincode,
                Phone = NormalizeIndianPhone(request.DropContactPhone),
                Address = new ProRoutingAddress
                {
                    Name = request.DropContactName,
                    Line1 = request.DropAddressLine1,
                    Line2 = request.DropAddressLine2,
                    City = request.DropCity,
                    State = request.DropState
                },
                Otp = request.DropCode
            },
            CallbackUrl = request.CallbackUrl,
            OrderCategory = request.OrderCategory,
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

    // ProRouting rejects E.164 (+91xxxxxxxxxx) and country-coded variants. Strip non-digits
    // and keep the last 10 — that's what their schema accepts for Indian numbers.
    private static string NormalizeIndianPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
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