using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

public sealed class ProRoutingQuotesResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("valid_until")]
    public string? ValidUntil { get; set; }

    [JsonPropertyName("quote_id")]
    public string? QuoteId { get; set; }

    [JsonPropertyName("quotes")]
    public List<ProRoutingLspQuote> Quotes { get; set; } = [];

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    public bool IsSuccess => Status == 1 && Quotes.Any();
}

public sealed class ProRoutingLspQuote
{
    [JsonPropertyName("lsp_id")]
    public string LspId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("logistics_seller")]
    public string LogisticsSeller { get; set; } = string.Empty;

    [JsonPropertyName("price_forward")]
    public decimal PriceForward { get; set; }

    [JsonPropertyName("price_rto")]
    public decimal PriceRto { get; set; }

    [JsonPropertyName("sla")]
    public int SlaMins { get; set; }

    [JsonPropertyName("pickup_eta")]
    public int PickupEtaMins { get; set; }
}