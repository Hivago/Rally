namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Cross-module service that returns the live order feed for the admin panel.
/// Implemented in Orders.Infrastructure, consumed by Users.Application (admin panel).
/// </summary>
public interface ILiveOrderFeedService
{
    Task<IReadOnlyList<LiveOrderRow>> GetLiveOrdersAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record LiveOrderRow(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string RestaurantName,
    string? RiderName,
    string Status,
    string FulfillmentType,
    int ItemCount,
    decimal Total,
    string Currency,
    DateTime CreatedAt);
