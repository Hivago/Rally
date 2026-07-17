using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

/// <summary>
/// Response of POST partner/order/status.
///
/// NOTE: this is NOT the same shape as <see cref="ProRoutingWebhookPayload"/>, even though both
/// describe the same order. The status response wraps the order in an envelope, names the id
/// "id" (not "order_id"), and calls the agent "rider" (not "agent"). Deserializing one into the
/// other silently yields null state/rider.
///
/// The envelope also reports failure as {"status":0,"message":"..."} with HTTP 200, so the HTTP
/// status code alone is not a success check — always test <see cref="Status"/> == 1.
/// </summary>
public sealed class ProRoutingStatusResponse
{
    /// <summary>1 = success, 0 = failure (with <see cref="Message"/> set). HTTP is 200 either way.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("order")]
    public ProRoutingStatusOrder? Order { get; set; }
}

public sealed class ProRoutingStatusOrder
{
    /// <summary>The provider task id — our DeliveryRequest.ExternalTaskId.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Our own order id, echoed back.</summary>
    [JsonPropertyName("client_order_id")]
    public string? ClientOrderId { get; set; }

    /// <summary>e.g. "Searching-for-agent", "Agent-assigned", "Order-picked-up", "Order-delivered".</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("rider")]
    public ProRoutingStatusRider? Rider { get; set; }

    [JsonPropertyName("lsp")]
    public ProRoutingStatusLsp? Lsp { get; set; }

    [JsonPropertyName("tracking_url")]
    public string? TrackingUrl { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }
}

public sealed class ProRoutingStatusRider
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
}

public sealed class ProRoutingStatusLsp
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
