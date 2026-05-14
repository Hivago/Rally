using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

/// <summary>
/// Status callback shape pushed by ProRouting to our /prorouting/status webhook
/// when a delivery transitions state. Per docs the order data is nested:
/// {
///   "status": 1,
///   "order": { "id": "...", "client_order_id": "...", "state": "Agent-assigned" }
/// }
/// </summary>
public sealed class ProRoutingStatusCallback
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("order")]
    public ProRoutingStatusCallbackOrder? Order { get; set; }
}

public sealed class ProRoutingStatusCallbackOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string? ClientOrderId { get; set; }

    [JsonPropertyName("network_order_id")]
    public string? NetworkOrderId { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("lsp_id")]
    public string? LspId { get; set; }

    [JsonPropertyName("logistics_seller")]
    public string? LogisticsSeller { get; set; }

    /// <summary>
    /// Provider's primary forward-leg price (LSP delivery charge before
    /// platform fee). May be missing on early-state callbacks.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("fees")]
    public ProRoutingCallbackFees? Fees { get; set; }

    /// <summary>
    /// Quoted route distance in km, reported back on the assignment callback.
    /// </summary>
    [JsonPropertyName("distance")]
    public decimal? Distance { get; set; }

    [JsonPropertyName("rider")]
    public ProRoutingCallbackRider? Rider { get; set; }

    [JsonPropertyName("url")]
    public string? TrackingUrl { get; set; }

    [JsonPropertyName("tracking_url")]
    public string? TrackingUrlAlt { get; set; }

    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }
}

public sealed class ProRoutingCallbackFees
{
    [JsonPropertyName("lsp")]
    public decimal? Lsp { get; set; }

    [JsonPropertyName("platform")]
    public decimal? Platform { get; set; }

    [JsonPropertyName("total_with_tax")]
    public decimal? TotalWithTax { get; set; }
}

/// <summary>
/// Track callback shape pushed by ProRouting to our /prorouting/track webhook
/// with bulk live GPS updates for every active order.
/// </summary>
public sealed class ProRoutingTrackCallback
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("orders")]
    public List<ProRoutingTrackCallbackOrder> Orders { get; set; } = new();
}

public sealed class ProRoutingTrackCallbackOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("network_order_id")]
    public string? NetworkOrderId { get; set; }

    [JsonPropertyName("client_order_id")]
    public string? ClientOrderId { get; set; }

    [JsonPropertyName("rider")]
    public ProRoutingCallbackRider? Rider { get; set; }

    [JsonPropertyName("url")]
    public string? TrackingUrl { get; set; }
}

public sealed class ProRoutingCallbackRider
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("last_location")]
    public ProRoutingCallbackLocation? LastLocation { get; set; }
}

public sealed class ProRoutingCallbackLocation
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    /// <summary>
    /// "yyyy-MM-dd HH:mm:ss" in IST per docs.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}
