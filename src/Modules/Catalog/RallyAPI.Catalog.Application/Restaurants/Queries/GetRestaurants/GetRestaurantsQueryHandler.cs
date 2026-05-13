// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurants/GetRestaurantsQueryHandler.cs

using MediatR;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurants;

internal sealed class GetRestaurantsQueryHandler
    : IRequestHandler<GetRestaurantsQuery, Result<List<RestaurantListResponse>>>
{
    private readonly IRestaurantQueryService _restaurantQueryService;

    public GetRestaurantsQueryHandler(IRestaurantQueryService restaurantQueryService)
    {
        _restaurantQueryService = restaurantQueryService;
    }

    public async Task<Result<List<RestaurantListResponse>>> Handle(
        GetRestaurantsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurants = await _restaurantQueryService.GetActiveRestaurantsAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            cancellationToken);

        IEnumerable<RestaurantSummary> filtered = restaurants;

        // Dietary filters
        if (request.PureVeg == true)
            filtered = filtered.Where(r => r.IsPureVeg);

        if (request.VeganFriendly == true)
            filtered = filtered.Where(r => r.IsVeganFriendly);

        if (request.JainOptions == true)
            filtered = filtered.Where(r => r.HasJainOptions);

        // Cuisine filter (comma-separated, match any)
        if (!string.IsNullOrWhiteSpace(request.Cuisines))
        {
            var cuisineList = request.Cuisines
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            filtered = filtered.Where(r =>
                r.CuisineTypes.Any(c => cuisineList.Contains(c, StringComparer.OrdinalIgnoreCase)));
        }

        // Open now filter
        if (request.OpenNow == true)
            filtered = filtered.Where(r => r.IsAcceptingOrders);

        // Sorting
        var sorted = request.Sort?.ToLowerInvariant() switch
        {
            "distance" => filtered.OrderBy(r => r.DistanceKm ?? double.MaxValue),
            "cost_asc" => filtered.OrderBy(r => r.MinOrderAmount),
            "cost_desc" => filtered.OrderByDescending(r => r.MinOrderAmount),
            "prep_time" => filtered.OrderBy(r => r.AvgPrepTimeMins),
            _ => request.Latitude.HasValue
                ? filtered.OrderBy(r => r.DistanceKm ?? double.MaxValue)
                : filtered.OrderBy(r => r.Name)
        };

        var response = sorted.Select(r => new RestaurantListResponse(
            r.Id,
            r.Name,
            r.AddressLine,
            r.Latitude,
            r.Longitude,
            r.IsAcceptingOrders,
            r.AcceptsPickup,
            r.AvgPrepTimeMins,
            r.OpeningTime.ToString("HH:mm"),
            r.ClosingTime.ToString("HH:mm"),
            r.CuisineTypes,
            r.IsPureVeg,
            r.IsVeganFriendly,
            r.HasJainOptions,
            r.MinOrderAmount,
            r.LogoUrl,
            r.DistanceKm
        )).ToList();

        return response;
    }
}