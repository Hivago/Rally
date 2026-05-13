// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurants/GetRestaurantsQuery.cs

using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurants;

public sealed record GetRestaurantsQuery(
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null,
    string? Cuisines = null,
    bool? PureVeg = null,
    bool? VeganFriendly = null,
    bool? JainOptions = null,
    bool? OpenNow = null,
    string? Sort = null
) : IRequest<Result<List<RestaurantListResponse>>>;

public sealed record RestaurantListResponse(
    Guid Id,
    string Name,
    string AddressLine,
    double Latitude,
    double Longitude,
    bool IsAcceptingOrders,
    bool AcceptsPickup,
    int AvgPrepTimeMins,
    string OpeningTime,
    string ClosingTime,
    List<string> CuisineTypes,
    bool IsPureVeg,
    bool IsVeganFriendly,
    bool HasJainOptions,
    decimal MinOrderAmount,
    string? LogoUrl,
    double? DistanceKm);