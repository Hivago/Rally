using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateDelivery;

public sealed class UpdateRestaurantDeliveryCommandValidator
    : AbstractValidator<UpdateRestaurantDeliveryCommand>
{
    public UpdateRestaurantDeliveryCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.DeliveryMode).IsInEnum();
    }
}
