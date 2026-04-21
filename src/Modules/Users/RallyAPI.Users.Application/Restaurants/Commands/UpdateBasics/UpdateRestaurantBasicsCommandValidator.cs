using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.UpdateBasics;

public sealed class UpdateRestaurantBasicsCommandValidator
    : AbstractValidator<UpdateRestaurantBasicsCommand>
{
    public UpdateRestaurantBasicsCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(255)
            .When(x => x.Name is not null);

        RuleFor(x => x.Phone)
            .Matches(@"^[6-9]\d{9}$")
            .WithMessage("Phone must be a valid 10-digit Indian mobile number.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.FssaiNumber)
            .Length(14, 20)
            .When(x => !string.IsNullOrWhiteSpace(x.FssaiNumber));

        RuleFor(x => x.AddressLine)
            .NotEmpty().MaximumLength(500)
            .When(x => x.AddressLine is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);

        When(x => x.Latitude.HasValue || x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude).NotNull().InclusiveBetween(6m, 38m);
            RuleFor(x => x.Longitude).NotNull().InclusiveBetween(68m, 98m);
        });
    }
}
