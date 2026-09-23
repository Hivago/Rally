// File: src/Modules/Catalog/RallyAPI.Catalog.Application/Restaurants/Queries/GetRestaurantReviews/GetRestaurantReviewsQuery.cs

using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantReviews;

public sealed record GetRestaurantReviewsQuery(Guid RestaurantId)
    : IRequest<Result<RestaurantReviewsResponse>>;

/// <summary>
/// Rating/reviewCount are null when the restaurant has no GooglePlaceId linked yet,
/// or when the upstream Google call fails — this is a normal, cacheable "no data"
/// state, not an error. Reviews is always a list, empty in the same cases.
/// </summary>
public sealed record RestaurantReviewsResponse(
    Guid RestaurantId,
    double? Rating,
    int? UserRatingCount,
    List<ReviewResponse> Reviews);

public sealed record ReviewResponse(
    string AuthorName,
    string? AuthorPhotoUrl,
    int Rating,
    string Text,
    string RelativeTimeDescription);
