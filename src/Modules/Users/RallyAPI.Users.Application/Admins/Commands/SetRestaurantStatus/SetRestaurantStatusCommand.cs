using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.SetRestaurantStatus;

public sealed record SetRestaurantStatusCommand(
    Guid RestaurantId,
    bool IsActive) : IRequest<Result>;
