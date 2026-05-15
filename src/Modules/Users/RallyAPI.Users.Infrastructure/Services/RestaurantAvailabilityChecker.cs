using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Infrastructure.Services;

internal sealed class RestaurantAvailabilityChecker : IRestaurantAvailabilityChecker
{
    private readonly IRestaurantTimeOffRepository _timeOffRepository;

    public RestaurantAvailabilityChecker(IRestaurantTimeOffRepository timeOffRepository)
    {
        _timeOffRepository = timeOffRepository;
    }

    public Task<bool> IsClosedForScheduledTimeOffAsync(
        Guid restaurantId,
        DateTime? momentUtc = null,
        CancellationToken cancellationToken = default)
        => _timeOffRepository.IsClosedAtAsync(
            restaurantId,
            momentUtc ?? DateTime.UtcNow,
            cancellationToken);
}
