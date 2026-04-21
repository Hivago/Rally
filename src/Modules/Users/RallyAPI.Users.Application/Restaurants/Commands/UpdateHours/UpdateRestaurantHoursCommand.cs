using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateHours;

public sealed record UpdateRestaurantHoursCommand(
    Guid RestaurantId,
    TimeOnly? OpeningTime,
    TimeOnly? ClosingTime,
    bool? UseCustomSchedule,
    List<WeeklyScheduleDayInput>? WeeklySchedule) : IRequest<Result>;

public sealed record WeeklyScheduleDayInput(
    DayOfWeek DayOfWeek,
    List<ScheduleSlotInput> Slots);

public sealed record ScheduleSlotInput(
    TimeOnly OpensAt,
    TimeOnly ClosesAt);
