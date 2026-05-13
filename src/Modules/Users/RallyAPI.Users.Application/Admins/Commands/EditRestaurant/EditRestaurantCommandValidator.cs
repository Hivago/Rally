using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.EditRestaurant;

public sealed class EditRestaurantCommandValidator : AbstractValidator<EditRestaurantCommand>
{
    public EditRestaurantCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .NotEmpty().WithMessage("Restaurant ID is required.");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Phone)
            .Matches(@"^\d{10}$").WithMessage("Phone must be a 10-digit number.")
            .When(x => x.Phone is not null);

        // Deprecated: percentage commission no longer supported. Only flat fee is used.
        // RuleFor(x => x.CommissionPercentage)
        //     .InclusiveBetween(0, 100).WithMessage("Commission percentage must be between 0 and 100.")
        //     .When(x => x.CommissionPercentage.HasValue);

        RuleFor(x => x.CommissionFlatFee)
            .GreaterThanOrEqualTo(0).WithMessage("Commission flat fee must not be negative.")
            .When(x => x.CommissionFlatFee.HasValue);

        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum order amount must not be negative.")
            .When(x => x.MinOrderAmount.HasValue);

        RuleFor(x => x.AvgPrepTimeMins)
            .InclusiveBetween(1, 180).WithMessage("Average prep time must be between 1 and 180 minutes.")
            .When(x => x.AvgPrepTimeMins.HasValue);

        RuleFor(x => x.FssaiNumber)
            .MaximumLength(50).WithMessage("FSSAI number must not exceed 50 characters.")
            .When(x => x.FssaiNumber is not null);
    }
}
