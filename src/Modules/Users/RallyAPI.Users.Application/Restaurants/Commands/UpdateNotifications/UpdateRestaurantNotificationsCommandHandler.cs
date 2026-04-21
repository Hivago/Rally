using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateNotifications;

internal sealed class UpdateRestaurantNotificationsCommandHandler
    : IRequestHandler<UpdateRestaurantNotificationsCommand, Result>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantNotificationsCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRestaurantNotificationsCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        var current = restaurant.Notifications;
        var next = NotificationPreferences.Create(
            request.EmailAlerts ?? current.EmailAlerts,
            request.BrowserNotifications ?? current.BrowserNotifications,
            request.OrderSound ?? current.OrderSound);

        var r = restaurant.SetNotificationPreferences(next);
        if (r.IsFailure) return r;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
