using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.ScheduleTimeOff;

public sealed record ScheduleTimeOffCommand(
    Guid OwnerId,
    Guid RestaurantId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason) : IRequest<Result<ScheduleTimeOffResponse>>;

public sealed record ScheduleTimeOffResponse(
    Guid Id,
    Guid RestaurantId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason);
