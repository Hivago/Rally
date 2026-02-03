using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

public sealed class ProRoutingWebhookPayload
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("lsp_id")]
    public string? LspId { get; set; }

    [JsonPropertyName("logistics_seller")]
    public string? LogisticsSeller { get; set; }

    [JsonPropertyName("agent")]
    public ProRoutingAgentInfo? Agent { get; set; }

    [JsonPropertyName("tracking_url")]
    public string? TrackingUrl { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }
}

public sealed class ProRoutingAgentInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
}