using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateNotifications;

public sealed record UpdateRestaurantNotificationsCommand(
    Guid RestaurantId,
    bool? EmailAlerts,
    bool? BrowserNotifications,
    bool? OrderSound) : IRequest<Result>;
