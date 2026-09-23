namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record ArrivedAtPickupRequest
{
    /// <summary>Rider's device GPS latitude at the moment they tap "Arrived at Restaurant". Optional — omitted if location permission was denied.</summary>
    public double? Latitude { get; init; }

    /// <summary>Rider's device GPS longitude at the same moment.</summary>
    public double? Longitude { get; init; }
}
