using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Owners.Queries.GetTimeOffs;

internal sealed class GetTimeOffsQueryHandler
    : IRequestHandler<GetTimeOffsQuery, Result<IReadOnlyList<TimeOffResponse>>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantTimeOffRepository _timeOffRepository;

    public GetTimeOffsQueryHandler(
        IRestaurantRepository restaurantRepository,
        IRestaurantTimeOffRepository timeOffRepository)
    {
        _restaurantRepository = restaurantRepository;
        _timeOffRepository = timeOffRepository;
    }

    public async Task<Result<IReadOnlyList<TimeOffResponse>>> Handle(
        GetTimeOffsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<IReadOnlyList<TimeOffResponse>>(
                Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.OwnerId != request.OwnerId)
            return Result.Failure<IReadOnlyList<TimeOffResponse>>(
                Error.Forbidden("You do not have access to this outlet."));

        var nowUtc = DateTime.UtcNow;
        var items = await _timeOffRepository.GetForRestaurantAsync(
            request.RestaurantId,
            request.IncludeCancelled,
            request.IncludePast,
            nowUtc,
            cancellationToken);

        var response = items
            .Select(t => new TimeOffResponse(
                t.Id,
                t.RestaurantId,
                t.StartsAt,
                t.EndsAt,
                t.Reason,
                t.CancelledAt,
                t.CreatedAt,
                t.IsActiveAt(nowUtc)))
            .ToList();

        return Result.Success<IReadOnlyList<TimeOffResponse>>(response);
    }
}
