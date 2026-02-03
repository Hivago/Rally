using System.Text.Json.Serialization;

namespace RallyAPI.Infrastructure.GoogleMaps.Models;

public sealed class DistanceMatrixResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("origin_addresses")]
    public List<string> OriginAddresses { get; set; } = [];

    [JsonPropertyName("destination_addresses")]
    public List<string> DestinationAddresses { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<DistanceMatrixRow> Rows { get; set; } = [];

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public sealed class DistanceMatrixRow
{
    [JsonPropertyName("elements")]
    public List<DistanceMatrixElement> Elements { get; set; } = [];
}

public sealed class DistanceMatrixElement
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public DistanceMatrixValue? Distance { get; set; }

    [JsonPropertyName("duration")]
    public DistanceMatrixValue? Duration { get; set; }

    [JsonPropertyName("duration_in_traffic")]
    public DistanceMatrixValue? DurationInTraffic { get; set; }
}

public sealed class DistanceMatrixValue
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}