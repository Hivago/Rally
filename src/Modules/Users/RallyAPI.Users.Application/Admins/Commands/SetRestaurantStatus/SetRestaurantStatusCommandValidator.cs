using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.SetRestaurantStatus;

public sealed class SetRestaurantStatusCommandValidator : AbstractValidator<SetRestaurantStatusCommand>
{
    public SetRestaurantStatusCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("Restaurant ID is required.");
    }
}
