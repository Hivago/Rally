namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record GetQuoteRequest
{
    public required Guid RestaurantId { get; init; }
    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public string? PickupPincode { get; init; }

    // Drop is optional: pickup orders have no delivery destination.
    public double? DropLatitude { get; init; }
    public double? DropLongitude { get; init; }
    public string? DropPincode { get; init; }
    public string? City { get; init; }
    public required decimal OrderAmount { get; init; }

    /// <summary>"Delivery" (default) or "Pickup".</summary>
    public string? FulfillmentType { get; init; }
}
