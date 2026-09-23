using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRestaurantLocationReviewQueueRepository
{
    Task<RestaurantLocationReviewQueue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if this restaurant already has an open (Pending) finding — used by the
    /// drift-detection event handler to avoid enqueuing duplicate findings on every sweep tick.
    /// </summary>
    Task<bool> HasPendingAsync(Guid restaurantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestaurantLocationReviewQueue>> GetByStatusAsync(
        RestaurantLocationReviewStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(RestaurantLocationReviewQueue entity, CancellationToken cancellationToken = default);
    void Update(RestaurantLocationReviewQueue entity);
}
