using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetProfile;

internal sealed class GetRestaurantProfileQueryHandler
    : IRequestHandler<GetRestaurantProfileQuery, Result<RestaurantProfileResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;

    public GetRestaurantProfileQueryHandler(IRestaurantRepository restaurantRepository)
    {
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result<RestaurantProfileResponse>> Handle(
        GetRestaurantProfileQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(
            request.RestaurantId,
            cancellationToken);

        if (restaurant is null)
            return Result.Failure<RestaurantProfileResponse>(
                Error.NotFound("Restaurant", request.RestaurantId));

        var response = new RestaurantProfileResponse(
            restaurant.Id,
            restaurant.Name,
            restaurant.Phone.GetFormatted(),
            restaurant.Email.Value,
            restaurant.AddressLine,
            restaurant.Latitude,
            restaurant.Longitude,
            restaurant.IsActive,
            restaurant.IsAcceptingOrders,
            restaurant.AvgPrepTimeMins,
            restaurant.OpeningTime,
            restaurant.ClosingTime,
            restaurant.CommissionPercentage,
            restaurant.CommissionFlatFee);

        return Result.Success(response);
    }
}