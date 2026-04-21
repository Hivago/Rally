using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdatePassword;

public sealed record UpdateRestaurantPasswordCommand(
    Guid RestaurantId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
