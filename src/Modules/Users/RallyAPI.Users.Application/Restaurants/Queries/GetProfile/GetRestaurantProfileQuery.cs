using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Queries.GetProfile;

public sealed record GetRestaurantProfileQuery(Guid RestaurantId)
    : IRequest<Result<RestaurantProfileResponse>>;

public sealed record RestaurantProfileResponse(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    string AddressLine,
    decimal Latitude,
    decimal Longitude,
    bool IsActive,
    bool IsAcceptingOrders,
    int AvgPrepTimeMins,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime,
    // decimal CommissionPercentage,   // Deprecated: percentage commission no longer surfaced. Only flat fee is used.
    decimal CommissionFlatFee);