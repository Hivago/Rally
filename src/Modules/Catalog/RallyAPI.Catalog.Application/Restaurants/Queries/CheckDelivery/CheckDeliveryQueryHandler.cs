using MediatR;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Utilities;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.CheckDelivery;

internal sealed class CheckDeliveryQueryHandler
    : IRequestHandler<CheckDeliveryQuery, Result<DeliveryCheckResponse>>
{
    private const double MaxDeliveryDistanceKm = 5.0;

    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IDistanceCalculator _distanceCalculator;

    public CheckDeliveryQueryHandler(
        IRestaurantQueryService restaurantQueryService,
        IDistanceCalculator distanceCalculator)
    {
        _restaurantQueryService = restaurantQueryService;
        _distanceCalculator = distanceCalculator;
    }

    public async Task<Result<DeliveryCheckResponse>> Handle(
        CheckDeliveryQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantQueryService.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<DeliveryCheckResponse>(Error.NotFound("Restaurant.NotFound", request.RestaurantId));

        var distanceResult = await _distanceCalculator.GetDistanceAsync(
            restaurant.Latitude, restaurant.Longitude,
            request.CustomerLat, request.CustomerLng,
            cancellationToken);

        var distanceKm = distanceResult.IsSuccess
            ? (double)distanceResult.DistanceKm
            : GeoCalculator.CalculateDistanceKm(
                restaurant.Latitude, restaurant.Longitude,
                request.CustomerLat, request.CustomerLng);

        return Result.Success(new DeliveryCheckResponse(
            CanDeliver: distanceKm <= MaxDeliveryDistanceKm,
            DistanceKm: Math.Round(distanceKm, 2),
            MaxDistanceKm: MaxDeliveryDistanceKm));
    }
}
