using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetDetails;

public sealed record GetRestaurantDetailsQuery(Guid RestaurantId)
    : IRequest<Result<RestaurantDetailsResponse>>;

public sealed record RestaurantDetailsResponse(
    Guid Id,
    RestaurantProfileSection Profile,
    RestaurantDietarySection Dietary,
    RestaurantOperationsSection Operations,
    RestaurantHoursSection Hours,
    RestaurantDeliverySection Delivery,
    RestaurantNotificationsSection Notifications);

public sealed record RestaurantProfileSection(
    string Name,
    string Phone,
    string Email,
    string? FssaiNumber,
    string AddressLine,
    decimal Latitude,
    decimal Longitude,
    string? Description,
    string? LogoUrl);

public sealed record RestaurantDietarySection(
    DietaryType DietaryType,
    bool IsPureVeg,
    bool IsVeganFriendly,
    bool HasJainOptions,
    List<string> CuisineTypes);

public sealed record RestaurantOperationsSection(
    bool IsActive,
    bool IsAcceptingOrders,
    bool AutoAcceptOrders,
    int AvgPrepTimeMins,
    decimal MinOrderAmount,
    decimal CommissionPercentage);

public sealed record RestaurantHoursSection(
    bool UseCustomSchedule,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime,
    IReadOnlyList<RestaurantScheduleDay> WeeklySchedule);

public sealed record RestaurantScheduleDay(
    DayOfWeek DayOfWeek,
    IReadOnlyList<RestaurantScheduleSlotDto> Slots);

public sealed record RestaurantScheduleSlotDto(
    TimeOnly OpensAt,
    TimeOnly ClosesAt);

public sealed record RestaurantDeliverySection(
    DeliveryMode DeliveryMode);

public sealed record RestaurantNotificationsSection(
    bool EmailAlerts,
    bool BrowserNotifications,
    bool OrderSound);
