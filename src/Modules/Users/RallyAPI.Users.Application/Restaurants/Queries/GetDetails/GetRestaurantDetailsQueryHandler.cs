using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetDetails;

internal sealed class GetRestaurantDetailsQueryHandler
    : IRequestHandler<GetRestaurantDetailsQuery, Result<RestaurantDetailsResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;

    public GetRestaurantDetailsQueryHandler(IRestaurantRepository restaurantRepository)
    {
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result<RestaurantDetailsResponse>> Handle(
        GetRestaurantDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdWithScheduleAsync(
            request.RestaurantId,
            cancellationToken);

        if (restaurant is null)
            return Result.Failure<RestaurantDetailsResponse>(
                Error.NotFound("Restaurant", request.RestaurantId));

        var weeklySchedule = Enum.GetValues<DayOfWeek>()
            .Select(day =>
            {
                var slots = restaurant.ScheduleSlots
                    .Where(s => s.DayOfWeek == day)
                    .OrderBy(s => s.OpensAt)
                    .Select(s => new RestaurantScheduleSlotDto(s.OpensAt, s.ClosesAt))
                    .ToList();

                return new RestaurantScheduleDay(day, slots);
            })
            .ToList();

        var response = new RestaurantDetailsResponse(
            restaurant.Id,
            new RestaurantProfileSection(
                restaurant.Name,
                restaurant.Phone.GetFormatted(),
                restaurant.Email.Value,
                restaurant.FssaiNumber,
                restaurant.AddressLine,
                restaurant.Latitude,
                restaurant.Longitude,
                restaurant.Description,
                restaurant.LogoUrl),
            new RestaurantDietarySection(
                restaurant.DietaryType,
                restaurant.IsPureVeg,
                restaurant.IsVeganFriendly,
                restaurant.HasJainOptions,
                restaurant.CuisineTypes.ToList()),
            new RestaurantOperationsSection(
                restaurant.IsActive,
                restaurant.IsAcceptingOrders,
                restaurant.AutoAcceptOrders,
                restaurant.AvgPrepTimeMins,
                restaurant.MinOrderAmount,
                restaurant.CommissionPercentage),
            new RestaurantHoursSection(
                restaurant.UseCustomSchedule,
                restaurant.OpeningTime,
                restaurant.ClosingTime,
                weeklySchedule),
            new RestaurantDeliverySection(restaurant.DeliveryMode),
            new RestaurantNotificationsSection(
                restaurant.Notifications.EmailAlerts,
                restaurant.Notifications.BrowserNotifications,
                restaurant.Notifications.OrderSound));

        return Result.Success(response);
    }
}
