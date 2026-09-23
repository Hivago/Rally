// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurants/GetRestaurantsQueryHandler.cs

using MediatR;
using RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantReviews;
using RallyAPI.SharedKernel.Abstractions.Caching;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurants;

internal sealed class GetRestaurantsQueryHandler
    : IRequestHandler<GetRestaurantsQuery, Result<PagedRestaurantsResponse>>
{
    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly ICacheService _cache;

    public GetRestaurantsQueryHandler(IRestaurantQueryService restaurantQueryService, ICacheService cache)
    {
        _restaurantQueryService = restaurantQueryService;
        _cache = cache;
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

        // Read-only lookup against the existing 24h reviews cache — never calls Google
        // here. A restaurant whose reviews aren't cached yet just shows a null rating
        // until the next visit to its detail page (or the daily refresh) populates it.
        var reviewsByPlaceId = await GetCachedReviewsAsync(paged.Items, cancellationToken);

        var items = paged.Items
            .Select(r =>
            {
                var reviews = r.GooglePlaceId is not null && reviewsByPlaceId.TryGetValue(r.GooglePlaceId, out var cached)
                    ? cached
                    : null;

                return new RestaurantListResponse(
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
                    r.DistanceKm,
                    reviews?.Rating,
                    reviews?.UserRatingCount);
            })
            .ToList();

        return new PagedRestaurantsResponse(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    private async Task<Dictionary<string, RestaurantReviewsResponse>> GetCachedReviewsAsync(
        IReadOnlyList<RestaurantSummary> items,
        CancellationToken cancellationToken)
    {
        var placeIds = items
            .Select(r => r.GooglePlaceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (placeIds.Count == 0)
            return new Dictionary<string, RestaurantReviewsResponse>();

        var lookups = await Task.WhenAll(placeIds.Select(async placeId =>
        {
            var cached = await _cache.GetAsync<RestaurantReviewsResponse>(
                CatalogCacheKeys.GoogleReviews(placeId!), cancellationToken);
            return (PlaceId: placeId!, Cached: cached);
        }));

        return lookups
            .Where(l => l.Cached is not null)
            .ToDictionary(l => l.PlaceId, l => l.Cached!);
    }
}
