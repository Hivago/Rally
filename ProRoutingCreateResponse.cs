using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

public sealed class ProRoutingCreateResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("order")]
    public ProRoutingOrderInfo? Order { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    public bool IsSuccess => Status == 1 && Order != null;
}

public sealed class ProRoutingOrderInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}