// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurantReviews/GetRestaurantReviewsQueryHandler.cs

using MediatR;
using RallyAPI.SharedKernel.Abstractions.Caching;
using RallyAPI.SharedKernel.Abstractions.Restaurants;
using RallyAPI.SharedKernel.Abstractions.Reviews;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantReviews;

internal sealed class GetRestaurantReviewsQueryHandler
    : IRequestHandler<GetRestaurantReviewsQuery, Result<RestaurantReviewsResponse>>
{
    // Google's own review data changes infrequently; a long TTL keeps the Atmosphere
    // Data SKU cost down (~$5/1000 calls) without users noticing stale ratings.
    private static readonly TimeSpan ReviewsTtl = TimeSpan.FromHours(24);

    private static readonly RestaurantReviewsResponse Empty =
        new(Guid.Empty, null, null, new List<ReviewResponse>());

    private readonly IRestaurantQueryService _restaurantQueryService;
    private readonly IGoogleReviewsService _googleReviewsService;
    private readonly ICacheService _cache;

    public GetRestaurantReviewsQueryHandler(
        IRestaurantQueryService restaurantQueryService,
        IGoogleReviewsService googleReviewsService,
        ICacheService cache)
    {
        _restaurantQueryService = restaurantQueryService;
        _googleReviewsService = googleReviewsService;
        _cache = cache;
    }

    public async Task<Result<RestaurantReviewsResponse>> Handle(
        GetRestaurantReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantQueryService.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<RestaurantReviewsResponse>(
                Error.NotFound("Restaurant.NotFound", request.RestaurantId));

        if (string.IsNullOrWhiteSpace(restaurant.GooglePlaceId))
            return Empty with { RestaurantId = request.RestaurantId };

        var placeId = restaurant.GooglePlaceId;
        var cacheKey = CatalogCacheKeys.GoogleReviews(placeId);

        var cached = await _cache.GetAsync<RestaurantReviewsResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached with { RestaurantId = request.RestaurantId };

        var result = await _googleReviewsService.GetPlaceReviewsAsync(placeId, cancellationToken);

        var response = result is null
            ? Empty with { RestaurantId = request.RestaurantId }
            : new RestaurantReviewsResponse(
                request.RestaurantId,
                result.Rating,
                result.UserRatingCount,
                result.Reviews.Select(r => new ReviewResponse(
                    r.AuthorName,
                    r.AuthorPhotoUrl,
                    r.Rating,
                    r.Text,
                    r.RelativeTimeDescription)).ToList());

        // Cache under the place ID (not the restaurant ID) so it's keyed on what
        // actually determines the content; RestaurantId is overwritten on read.
        await _cache.SetAsync(cacheKey, response, ReviewsTtl, cancellationToken);

        return response;
    }
}
