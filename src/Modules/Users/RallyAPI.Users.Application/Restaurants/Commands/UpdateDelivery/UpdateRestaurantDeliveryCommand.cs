using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDelivery;

public sealed record UpdateRestaurantDeliveryCommand(
    Guid RestaurantId,
    DeliveryMode DeliveryMode) : IRequest<Result>;
