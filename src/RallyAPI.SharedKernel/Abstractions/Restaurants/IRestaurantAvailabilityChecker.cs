namespace RallyAPI.SharedKernel.Abstractions.Restaurants;

/// <summary>
/// Cross-module probe for whether a restaurant is currently available to take orders.
/// Implemented in Users.Infrastructure; consumed by Orders.Application (PlaceOrder) and
/// anywhere else that needs an authoritative "is this outlet open right now" answer that
/// takes scheduled time off into account.
/// </summary>
public interface IRestaurantAvailabilityChecker
{
    /// <summary>
    /// Returns true if the restaurant has an active (non-cancelled) scheduled time off
    /// covering <paramref name="momentUtc"/> (defaults to <c>DateTime.UtcNow</c>).
    /// </summary>
    Task<bool> IsClosedForScheduledTimeOffAsync(
        Guid restaurantId,
        DateTime? momentUtc = null,
        CancellationToken cancellationToken = default);
}
