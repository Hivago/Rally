using System.Text.Json.Serialization;

namespace RallyAPI.Integrations.ProRouting.Models;

/// <summary>
/// Location model for ProRouting API requests.
/// </summary>
public sealed class ProRoutingLocation
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("pincode")]
    public string Pincode { get; set; } = string.Empty;
}