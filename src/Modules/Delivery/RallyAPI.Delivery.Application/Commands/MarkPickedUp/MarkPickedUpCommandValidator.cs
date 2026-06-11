using FluentValidation;

namespace RallyAPI.Delivery.Application.Commands.MarkPickedUp;

public sealed class MarkPickedUpCommandValidator : AbstractValidator<MarkPickedUpCommand>
{
    public MarkPickedUpCommandValidator()
    {
        RuleFor(x => x.DeliveryRequestId)
            .NotEmpty()
            .WithMessage("Delivery request ID is required");

        RuleFor(x => x.RiderId)
            .NotEmpty()
            .WithMessage("Rider ID is required");

        RuleFor(x => x.PickupCode)
            .NotEmpty()
            .WithMessage("Pickup code is required")
            .Matches(@"^\d{4}$")
            .WithMessage("Pickup code must be 4 digits");
    }
}
