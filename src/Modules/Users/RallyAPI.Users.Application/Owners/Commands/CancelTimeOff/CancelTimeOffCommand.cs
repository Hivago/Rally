using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.CancelTimeOff;

public sealed record CancelTimeOffCommand(
    Guid OwnerId,
    Guid RestaurantId,
    Guid TimeOffId) : IRequest<Result>;
