using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Admins.Queries.ListRestaurantLocationReviews;

internal sealed class ListRestaurantLocationReviewsQueryHandler
    : IRequestHandler<ListRestaurantLocationReviewsQuery, Result<ListRestaurantLocationReviewsResponse>>
{
    private readonly IRestaurantLocationReviewQueueRepository _reviewQueueRepository;
    private readonly IRestaurantRepository _restaurantRepository;

    public ListRestaurantLocationReviewsQueryHandler(
        IRestaurantLocationReviewQueueRepository reviewQueueRepository,
        IRestaurantRepository restaurantRepository)
    {
        _reviewQueueRepository = reviewQueueRepository;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result<ListRestaurantLocationReviewsResponse>> Handle(
        ListRestaurantLocationReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var entries = await _reviewQueueRepository.GetByStatusAsync(
            request.Status, page, pageSize, cancellationToken);

        // Low-volume admin screen (pageSize <= 100, findings are rare) — a per-row
        // lookup is fine here, no need for a bulk-fetch abstraction.
        var items = new List<RestaurantLocationReviewItem>(entries.Count);
        foreach (var entry in entries)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(entry.RestaurantId, cancellationToken);
            items.Add(new RestaurantLocationReviewItem(
                entry.Id,
                entry.RestaurantId,
                restaurant?.Name ?? "(deleted restaurant)",
                entry.CurrentLatitude,
                entry.CurrentLongitude,
                entry.SuggestedLatitude,
                entry.SuggestedLongitude,
                entry.AverageDriftMeters,
                entry.SampleSize,
                entry.Status.ToString(),
                entry.DetectedAt));
        }

        return Result.Success(new ListRestaurantLocationReviewsResponse(items, page, pageSize));
    }
}
