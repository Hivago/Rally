using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Owners.Commands.ScheduleTimeOff;

namespace RallyAPI.Users.Application.Owners.Commands.QuickPauseOutlet;

/// <summary>
/// Pause the outlet for <paramref name="DurationMinutes"/> starting now. Convenience wrapper
/// around <see cref="ScheduleTimeOffCommand"/> — backing storage is identical.
/// </summary>
public sealed record QuickPauseOutletCommand(
    Guid OwnerId,
    Guid RestaurantId,
    int DurationMinutes,
    string? Reason) : IRequest<Result<ScheduleTimeOffResponse>>;
