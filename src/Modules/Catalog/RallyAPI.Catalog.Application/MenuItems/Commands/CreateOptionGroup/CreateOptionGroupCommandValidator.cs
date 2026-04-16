using FluentValidation;
using RallyAPI.Catalog.Application.Abstractions.Validation;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.CreateOptionGroup;

public sealed class CreateOptionGroupCommandValidator : AbstractValidator<CreateOptionGroupCommand>
{
    public CreateOptionGroupCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");

        RuleFor(x => x.MenuItemId)
            .NotEmpty().WithMessage("MenuItemId is required.");

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

        RuleForEach(x => x.Options)
            .SetValidator(new OptionItemDtoValidator())
            .When(x => x.Options is not null);
    }
}

internal sealed class OptionItemDtoValidator : AbstractValidator<OptionItemDto>
{
    public OptionItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Option Name is required.")
            .MaximumLength(100).WithMessage("Option Name must be 100 characters or fewer.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Option Type is required.")
            .Must(OptionTypeRules.IsValid)
                .WithMessage(x => $"Option Type '{x.Type}' is invalid. Allowed values: {OptionTypeRules.AllowedTypes}.");

        RuleFor(x => x.AdditionalPrice)
            .GreaterThanOrEqualTo(0).WithMessage("AdditionalPrice cannot be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .WithMessage("AdditionalPrice must have at most 2 decimal places.");
    }
}
