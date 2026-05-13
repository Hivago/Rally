// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurants/GetRestaurantsQueryHandler.cs

using MediatR;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurants;

internal sealed class GetRestaurantsQueryHandler
    : IRequestHandler<GetRestaurantsQuery, Result<PagedRestaurantsResponse>>
{
    private readonly IRestaurantQueryService _restaurantQueryService;

    public GetRestaurantsQueryHandler(IRestaurantQueryService restaurantQueryService)
    {
        _restaurantQueryService = restaurantQueryService;
    }

    public async Task<Result<PagedRestaurantsResponse>> Handle(
        GetRestaurantsQuery request,
        CancellationToken cancellationToken)
    {
        var cuisines = string.IsNullOrWhiteSpace(request.Cuisines)
            ? null
            : request.Cuisines
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        var filter = new RestaurantListFilter
        {
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RadiusKm = request.RadiusKm,
            Search = request.Search,
            Cuisines = cuisines,
            PureVeg = request.PureVeg,
            VeganFriendly = request.VeganFriendly,
            JainOptions = request.JainOptions,
            OpenNow = request.OpenNow,
            MaxPrepTimeMins = request.MaxPrepTimeMins,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            SupportsPickup = request.SupportsPickup,
            Sort = request.Sort,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var paged = await _restaurantQueryService.BrowseAsync(filter, cancellationToken);

        var items = paged.Items
            .Select(r => new RestaurantListResponse(
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
                r.DistanceKm))
            .ToList();

        return new PagedRestaurantsResponse(items, paged.TotalCount, paged.Page, paged.PageSize);
    }
}
