using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.AddOutlet;

public sealed class AddOutletCommandValidator : AbstractValidator<AddOutletCommand>
{
    public AddOutletCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.AddressLine).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(6m, 38m)
            .WithMessage("Latitude must be within India bounds.");
        RuleFor(x => x.Longitude).InclusiveBetween(68m, 98m)
            .WithMessage("Longitude must be within India bounds.");
        RuleFor(x => x.FssaiNumber)
            .Length(14, 20)
            .When(x => !string.IsNullOrEmpty(x.FssaiNumber));
    }
}
