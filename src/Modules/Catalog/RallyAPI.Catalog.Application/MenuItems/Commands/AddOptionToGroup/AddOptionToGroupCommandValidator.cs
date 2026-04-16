using FluentValidation;
using RallyAPI.Catalog.Application.Abstractions.Validation;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.AddOptionToGroup;

public sealed class AddOptionToGroupCommandValidator : AbstractValidator<AddOptionToGroupCommand>
{
    public AddOptionToGroupCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");

        RuleFor(x => x.OptionGroupId)
            .NotEmpty().WithMessage("OptionGroupId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(OptionTypeRules.IsValid)
                .WithMessage(x => $"Type '{x.Type}' is invalid. Allowed values: {OptionTypeRules.AllowedTypes}.");

        RuleFor(x => x.AdditionalPrice)
            .GreaterThanOrEqualTo(0).WithMessage("AdditionalPrice cannot be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .WithMessage("AdditionalPrice must have at most 2 decimal places.");
    }
}
