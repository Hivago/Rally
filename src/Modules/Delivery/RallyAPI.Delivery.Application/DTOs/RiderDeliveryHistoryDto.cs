namespace RallyAPI.Delivery.Application.DTOs;

/// <summary>
/// One completed delivery in the rider's history tab.
/// </summary>
public sealed record RiderDeliveryHistoryItemDto
{
    public required Guid DeliveryRequestId { get; init; }
    public required string OrderNumber { get; init; }
    public required string RestaurantName { get; init; }
    public required string DropAddress { get; init; }
    public required string DropPincode { get; init; }
    public required decimal Earnings { get; init; }
    public decimal? DistanceKm { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// Paginated rider delivery history.
/// </summary>
public sealed record RiderDeliveryHistoryResponse
{
    public required IReadOnlyList<RiderDeliveryHistoryItemDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
