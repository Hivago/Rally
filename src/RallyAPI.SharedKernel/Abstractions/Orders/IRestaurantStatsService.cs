namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Cross-module service that returns operational stats for a single restaurant outlet.
/// Powers the restaurant dashboard summary endpoint.
/// Implemented in Orders.Infrastructure, consumed by Orders.Application.
/// </summary>
public interface IRestaurantStatsService
{
    /// <summary>
    /// Returns a snapshot of orders for <paramref name="restaurantId"/> since <paramref name="fromUtc"/>.
    /// Range counts (total / delivered / cancelled / revenue) use the time window;
    /// the active snapshot is right-now regardless of the window.
    /// </summary>
    Task<RestaurantStatsSnapshot> GetStatsAsync(
        Guid restaurantId,
        DateTime fromUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RestaurantStatsSnapshot(
    int OrdersTotal,
    int OrdersDelivered,
    int OrdersCancelled,
    int OrdersActive,
    decimal GrossRevenue,
    decimal AverageOrderValue,
    IReadOnlyDictionary<string, int> ActiveByStatus);
