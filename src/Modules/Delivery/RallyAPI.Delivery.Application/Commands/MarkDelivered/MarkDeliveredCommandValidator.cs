using FluentValidation;

namespace RallyAPI.Delivery.Application.Commands.MarkDelivered;

public sealed class MarkDeliveredCommandValidator : AbstractValidator<MarkDeliveredCommand>
{
    public MarkDeliveredCommandValidator()
    {
        RuleFor(x => x.DeliveryRequestId)
            .NotEmpty()
            .WithMessage("Delivery request ID is required");

        RuleFor(x => x.RiderId)
            .NotEmpty()
            .WithMessage("Rider ID is required");

        RuleFor(x => x.DropCode)
            .NotEmpty()
            .WithMessage("Delivery code is required")
            .Matches(@"^\d{4}$")
            .WithMessage("Delivery code must be 4 digits");
    }
}
