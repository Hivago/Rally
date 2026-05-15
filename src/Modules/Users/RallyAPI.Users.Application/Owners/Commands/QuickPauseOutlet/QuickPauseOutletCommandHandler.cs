using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Owners.Commands.ScheduleTimeOff;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Owners.Commands.QuickPauseOutlet;

internal sealed class QuickPauseOutletCommandHandler
    : IRequestHandler<QuickPauseOutletCommand, Result<ScheduleTimeOffResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantTimeOffRepository _timeOffRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QuickPauseOutletCommandHandler(
        IRestaurantRepository restaurantRepository,
        IRestaurantTimeOffRepository timeOffRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _timeOffRepository = timeOffRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ScheduleTimeOffResponse>> Handle(
        QuickPauseOutletCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<ScheduleTimeOffResponse>(Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.OwnerId != request.OwnerId)
            return Result.Failure<ScheduleTimeOffResponse>(
                Error.Forbidden("You do not have access to this outlet."));

        var nowUtc = DateTime.UtcNow;
        var startsAtUtc = nowUtc;
        var endsAtUtc = nowUtc.AddMinutes(request.DurationMinutes);

        if (await _timeOffRepository.HasOverlapAsync(
                request.RestaurantId, startsAtUtc, endsAtUtc, excludeId: null, cancellationToken))
        {
            return Result.Failure<ScheduleTimeOffResponse>(
                Error.Conflict("Another scheduled time off overlaps this window. Cancel it first to pause again."));
        }

        var createResult = RestaurantTimeOff.Create(
            request.RestaurantId,
            startsAtUtc,
            endsAtUtc,
            request.Reason,
            request.OwnerId,
            nowUtc);

        if (createResult.IsFailure)
            return Result.Failure<ScheduleTimeOffResponse>(createResult.Error);

        var timeOff = createResult.Value;
        await _timeOffRepository.AddAsync(timeOff, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ScheduleTimeOffResponse(
            timeOff.Id,
            timeOff.RestaurantId,
            timeOff.StartsAt,
            timeOff.EndsAt,
            timeOff.Reason);
    }
}
