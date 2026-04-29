namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Cross-module service for order aggregate counts and revenue.
/// Implemented in Orders.Infrastructure, consumed by Users.Application (admin stats).
/// </summary>
public interface IOrderStatsService
{
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetTodayCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts orders created since (and including) the given UTC instant.
    /// </summary>
    Task<int> GetCountSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums Pricing.Total for orders created since the given UTC instant.
    /// Excludes orders in Pending/Failed/Refunded states (no realized revenue).
    /// </summary>
    Task<decimal> GetRevenueSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums commission earned (from payout ledger) for orders delivered since the given UTC instant.
    /// </summary>
    Task<decimal> GetCommissionSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifetime commission earned (commission_amount across all ledger entries).
    /// </summary>
    Task<decimal> GetLifetimeCommissionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifetime gross revenue (Pricing.Total) across all non-cancelled/non-pending orders.
    /// </summary>
    Task<decimal> GetLifetimeRevenueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns order count grouped by status name.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetOrdersByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of payout ledger entries that have not yet been batched into a payout.
    /// </summary>
    Task<int> GetPendingLedgerEntriesCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum of net amount owed to restaurants from payout ledger entries that have not yet been paid out.
    /// </summary>
    Task<decimal> GetUnpaidPayoutAmountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total order count for each given restaurant id in a single round trip.
    /// Restaurants with zero orders are omitted from the dictionary; callers should default to 0.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetOrderCountsByRestaurantAsync(
        IReadOnlyCollection<Guid> restaurantIds,
        CancellationToken cancellationToken = default);
}
