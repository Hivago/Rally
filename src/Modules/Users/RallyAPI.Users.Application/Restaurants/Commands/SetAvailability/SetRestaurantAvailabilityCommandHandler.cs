using MediatR;
using RallyAPI.SharedKernel.Abstractions.Caching;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.SetAvailability;

internal sealed class SetRestaurantAvailabilityCommandHandler
    : IRequestHandler<SetRestaurantAvailabilityCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public SetRestaurantAvailabilityCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result> Handle(
        SetRestaurantAvailabilityCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(
            request.RestaurantId,
            cancellationToken);

        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        var result = request.IsAcceptingOrders
            ? restaurant.StartAcceptingOrders()
            : restaurant.StopAcceptingOrders();

        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Open/close must reflect on customer browse + menu immediately, not
        // after the cache TTL — a closed restaurant still shown open takes orders
        // it can't fulfil.
        await _cache.RemoveAsync(CatalogCacheKeys.ActiveRestaurants, cancellationToken);
        await _cache.RemoveAsync(CatalogCacheKeys.Menu(request.RestaurantId), cancellationToken);

        return Result.Success();
    }
}