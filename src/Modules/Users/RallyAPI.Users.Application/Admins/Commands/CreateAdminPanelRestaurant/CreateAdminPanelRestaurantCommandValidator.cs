using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.CreateAdminPanelRestaurant;

public sealed class CreateAdminPanelRestaurantCommandValidator
    : AbstractValidator<CreateAdminPanelRestaurantCommand>
{
    public CreateAdminPanelRestaurantCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId)
            .NotEmpty().WithMessage("Requesting admin ID is required.");

        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("Owner ID is required. Pick an existing owner or create one first.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name is too long.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500).WithMessage("Address is too long.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(6, 38).WithMessage("Invalid latitude for India.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(68, 98).WithMessage("Invalid longitude for India.");
    }
}
