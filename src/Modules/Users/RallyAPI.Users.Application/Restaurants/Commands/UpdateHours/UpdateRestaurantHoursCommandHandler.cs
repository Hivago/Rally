using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateHours;

internal sealed class UpdateRestaurantHoursCommandHandler
    : IRequestHandler<UpdateRestaurantHoursCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantHoursCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantHoursCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdWithScheduleAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (request.OpeningTime.HasValue && request.ClosingTime.HasValue)
        {
            var r = restaurant.SetBusinessHours(request.OpeningTime.Value, request.ClosingTime.Value);
            if (r.IsFailure) return r;
        }

        if (request.UseCustomSchedule.HasValue)
        {
            var r = restaurant.SetUseCustomSchedule(request.UseCustomSchedule.Value);
            if (r.IsFailure) return r;
        }

        if (request.WeeklySchedule is not null)
        {
            foreach (var day in request.WeeklySchedule)
            {
                var slots = (day.Slots ?? new())
                    .Select(s => (s.OpensAt, s.ClosesAt))
                    .ToList();

                var r = restaurant.ReplaceScheduleForDay(day.DayOfWeek, slots);
                if (r.IsFailure) return r;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
