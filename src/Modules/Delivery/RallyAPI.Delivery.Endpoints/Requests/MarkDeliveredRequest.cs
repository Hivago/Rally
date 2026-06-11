namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record MarkDeliveredRequest
{
    /// <summary>4-digit code the customer reads out to the rider at the door.</summary>
    public required string DropCode { get; init; }
}
