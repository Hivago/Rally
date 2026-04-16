using FluentValidation;
using RallyAPI.Catalog.Application.Abstractions.Validation;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;

public sealed class CreateMenuItemCommandValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");

        RuleFor(x => x.MenuId)
            .NotEmpty().WithMessage("MenuId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must be 2000 characters or fewer.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.BasePrice)
            .GreaterThan(0).WithMessage("BasePrice must be greater than 0.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .WithMessage("BasePrice must have at most 2 decimal places.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2048).WithMessage("ImageUrl must be 2048 characters or fewer.")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("DisplayOrder cannot be negative.");

        RuleFor(x => x.PreparationTimeMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("PreparationTimeMinutes cannot be negative.")
            .LessThanOrEqualTo(240).WithMessage("PreparationTimeMinutes must be 240 or fewer.");

        RuleForEach(x => x.Options)
            .SetValidator(new MenuItemOptionDtoValidator())
            .When(x => x.Options is not null);

        RuleForEach(x => x.OptionGroups)
            .SetValidator(new OptionGroupDtoValidator())
            .When(x => x.OptionGroups is not null);

        RuleForEach(x => x.Tags)
            .NotEmpty().WithMessage("Tag cannot be empty.")
            .MaximumLength(50).WithMessage("Tag must be 50 characters or fewer.")
            .When(x => x.Tags is not null);
    }
}

internal sealed class MenuItemOptionDtoValidator : AbstractValidator<MenuItemOptionDto>
{
    public MenuItemOptionDtoValidator()
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

internal sealed class OptionGroupDtoValidator : AbstractValidator<OptionGroupDto>
{
    public OptionGroupDtoValidator()
    {
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

        RuleFor(x => x.Options)
            .NotNull().WithMessage("Options are required for an option group.")
            .Must(o => o is not null && o.Count > 0)
                .WithMessage("OptionGroup must have at least one option.");

        RuleForEach(x => x.Options)
            .SetValidator(new MenuItemOptionDtoValidator())
            .When(x => x.Options is not null);
    }
}
