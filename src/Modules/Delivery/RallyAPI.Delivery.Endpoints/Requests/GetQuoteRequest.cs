namespace RallyAPI.Delivery.Endpoints.Requests;

public sealed record GetQuoteRequest
{
    public required Guid RestaurantId { get; init; }
    public required double PickupLatitude { get; init; }
    public required double PickupLongitude { get; init; }
    public required string PickupPincode { get; init; }
    public required double DropLatitude { get; init; }
    public required double DropLongitude { get; init; }
    public required string DropPincode { get; init; }
    public required string City { get; init; }
    public required decimal OrderAmount { get; init; }
}