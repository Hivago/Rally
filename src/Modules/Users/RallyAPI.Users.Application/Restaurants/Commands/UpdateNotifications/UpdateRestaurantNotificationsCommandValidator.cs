using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateNotifications;

public sealed class UpdateRestaurantNotificationsCommandValidator
    : AbstractValidator<UpdateRestaurantNotificationsCommand>
{
    public UpdateRestaurantNotificationsCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
    }
}
