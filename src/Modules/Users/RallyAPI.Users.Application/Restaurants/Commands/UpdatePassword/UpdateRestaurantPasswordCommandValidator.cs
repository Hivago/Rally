using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdatePassword;

public sealed class UpdateRestaurantPasswordCommandValidator
    : AbstractValidator<UpdateRestaurantPasswordCommand>
{
    public UpdateRestaurantPasswordCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128);
    }
}
