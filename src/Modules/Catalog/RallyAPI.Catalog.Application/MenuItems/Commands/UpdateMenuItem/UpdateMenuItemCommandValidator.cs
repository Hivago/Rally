using FluentValidation;
using RallyAPI.Catalog.Application.Abstractions.Validation;
using RallyAPI.Catalog.Application.MenuItems.Commands.CreateMenuItem;

namespace RallyAPI.Catalog.Application.MenuItems.Commands.UpdateMenuItem;

public sealed class UpdateMenuItemCommandValidator : AbstractValidator<UpdateMenuItemCommand>
{
    public UpdateMenuItemCommandValidator()
    {
        RuleFor(x => x.MenuItemId)
            .NotEmpty().WithMessage("MenuItemId is required.");

        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("RestaurantId is required.");

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
            .ChildRules(opt =>
            {
                opt.RuleFor(o => o.Name)
                    .NotEmpty().WithMessage("Option Name is required.")
                    .MaximumLength(100);
                opt.RuleFor(o => o.Type)
                    .NotEmpty().WithMessage("Option Type is required.")
                    .Must(OptionTypeRules.IsValid)
                        .WithMessage(o => $"Option Type '{o.Type}' is invalid. Allowed values: {OptionTypeRules.AllowedTypes}.");
                opt.RuleFor(o => o.AdditionalPrice)
                    .GreaterThanOrEqualTo(0);
            })
            .When(x => x.Options is not null);

        RuleForEach(x => x.OptionGroups)
            .ChildRules(grp =>
            {
                grp.RuleFor(g => g.GroupName).NotEmpty().MaximumLength(100);
                grp.RuleFor(g => g.MinSelections).GreaterThanOrEqualTo(0);
                grp.RuleFor(g => g.MaxSelections)
                    .GreaterThanOrEqualTo(g => g.MinSelections)
                    .WithMessage("MaxSelections must be greater than or equal to MinSelections.");
                grp.RuleFor(g => g.DisplayOrder).GreaterThanOrEqualTo(0);
                grp.RuleFor(g => g.Options)
                    .NotNull().Must(o => o is not null && o.Count > 0)
                    .WithMessage("OptionGroup must have at least one option.");
                grp.RuleForEach(g => g.Options).ChildRules(opt =>
                {
                    opt.RuleFor(o => o.Name).NotEmpty().MaximumLength(100);
                    opt.RuleFor(o => o.Type)
                        .NotEmpty()
                        .Must(OptionTypeRules.IsValid)
                            .WithMessage(o => $"Option Type '{o.Type}' is invalid. Allowed values: {OptionTypeRules.AllowedTypes}.");
                    opt.RuleFor(o => o.AdditionalPrice).GreaterThanOrEqualTo(0);
                });
            })
            .When(x => x.OptionGroups is not null);

        RuleForEach(x => x.Tags)
            .NotEmpty().WithMessage("Tag cannot be empty.")
            .MaximumLength(50).WithMessage("Tag must be 50 characters or fewer.")
            .When(x => x.Tags is not null);
    }
}
