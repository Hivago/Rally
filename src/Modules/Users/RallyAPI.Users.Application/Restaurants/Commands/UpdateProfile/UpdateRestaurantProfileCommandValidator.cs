using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateProfile;

public sealed class UpdateRestaurantProfileCommandValidator
    : AbstractValidator<UpdateRestaurantProfileCommand>
{
    public UpdateRestaurantProfileCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        RuleFor(x => x.Name)
            .MaximumLength(255)
            .When(x => x.Name is not null);

        RuleFor(x => x.AddressLine)
            .MaximumLength(500)
            .When(x => x.AddressLine is not null);

        RuleFor(x => x.Phone)
            .Matches(@"^[6-9]\d{9}$")
            .WithMessage("Phone must be a valid 10-digit Indian mobile number.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.MinOrderAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinOrderAmount.HasValue);

        RuleFor(x => x.FssaiNumber)
            .Length(14, 20)
            .When(x => !string.IsNullOrWhiteSpace(x.FssaiNumber));

        RuleFor(x => x.CuisineTypes)
            .Must(c => c!.Count <= 20)
            .WithMessage("Maximum 20 cuisine types allowed.")
            .When(x => x.CuisineTypes is not null);
    }
}
