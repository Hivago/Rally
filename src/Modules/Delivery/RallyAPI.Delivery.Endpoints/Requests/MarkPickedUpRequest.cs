namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record MarkPickedUpRequest
{
    /// <summary>4-digit code the restaurant reads out to the rider at pickup.</summary>
    public required string PickupCode { get; init; }
}
