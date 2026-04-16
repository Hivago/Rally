using FluentValidation;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateOptionGroup;

public sealed class UpdateOptionGroupCommandValidator : AbstractValidator<UpdateOptionGroupCommand>
{
    public UpdateOptionGroupCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");

        RuleFor(x => x.MenuItemId)
            .NotEmpty().WithMessage("MenuItemId is required.");

        RuleFor(x => x.OptionGroupId)
            .NotEmpty().WithMessage("OptionGroupId is required.");

        RuleFor(x => x.GroupName)
            .NotEmpty().WithMessage("GroupName is required.")
            .MaximumLength(100).WithMessage("GroupName must be 100 characters or fewer.");

        RuleFor(x => x.MinSelections)
            .GreaterThanOrEqualTo(0).WithMessage("MinSelections cannot be negative.");

        RuleFor(x => x.MaxSelections)
            .GreaterThanOrEqualTo(x => x.MinSelections)
                .WithMessage("MaxSelections must be greater than or equal to MinSelections.");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("DisplayOrder cannot be negative.");
    }
}
