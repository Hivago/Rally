using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Admins.Queries.ListRestaurantLocationReviews;

public sealed record ListRestaurantLocationReviewsQuery(
    RestaurantLocationReviewStatus Status,
    int Page,
    int PageSize) : IRequest<Result<ListRestaurantLocationReviewsResponse>>;

public sealed record RestaurantLocationReviewItem(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    decimal CurrentLatitude,
    decimal CurrentLongitude,
    decimal SuggestedLatitude,
    decimal SuggestedLongitude,
    decimal AverageDriftMeters,
    int SampleSize,
    string Status,
    DateTimeOffset DetectedAt);

public sealed record ListRestaurantLocationReviewsResponse(
    IReadOnlyList<RestaurantLocationReviewItem> Items,
    int Page,
    int PageSize);
