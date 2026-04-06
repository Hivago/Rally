using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Admins.Commands.DeactivateRestaurant;

public sealed record DeactivateRestaurantCommand(
    Guid RestaurantId) : IRequest<Result>;
