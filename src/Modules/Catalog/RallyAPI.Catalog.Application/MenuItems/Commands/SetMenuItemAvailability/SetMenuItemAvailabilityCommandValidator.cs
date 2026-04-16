using FluentValidation;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.SetMenuItemAvailability;

public sealed class SetMenuItemAvailabilityCommandValidator : AbstractValidator<SetMenuItemAvailabilityCommand>
{
    public SetMenuItemAvailabilityCommandValidator()
    {
        RuleFor(x => x.MenuItemId)
            .NotEmpty().WithMessage("MenuItemId is required.");

        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");
    }
}
