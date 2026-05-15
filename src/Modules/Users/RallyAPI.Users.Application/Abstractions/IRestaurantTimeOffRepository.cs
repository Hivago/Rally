using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRestaurantTimeOffRepository
{
    Task<RestaurantTimeOff?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestaurantTimeOff>> GetForRestaurantAsync(
        Guid restaurantId,
        bool includeCancelled,
        bool includePast,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any non-cancelled time off for <paramref name="restaurantId"/> overlaps
    /// the half-open window [startsAt, endsAt). Used to enforce non-overlap at scheduling time.
    /// </summary>
    Task<bool> HasOverlapAsync(
        Guid restaurantId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if there is an active (non-cancelled) time off covering <paramref name="momentUtc"/>.
    /// Hot path: called on every PlaceOrder.
    /// </summary>
    Task<bool> IsClosedAtAsync(
        Guid restaurantId,
        DateTime momentUtc,
        CancellationToken cancellationToken = default);

    Task AddAsync(RestaurantTimeOff entity, CancellationToken cancellationToken = default);
    void Update(RestaurantTimeOff entity);
}
