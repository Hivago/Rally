using FluentValidation;

namespace RallyAPI.Delivery.Application.Commands.DeclineDeliveryOffer;

public sealed class DeclineDeliveryOfferCommandValidator : AbstractValidator<DeclineDeliveryOfferCommand>
{
    public DeclineDeliveryOfferCommandValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty()
            .WithMessage("Offer ID is required");

        RuleFor(x => x.RiderId)
            .NotEmpty()
            .WithMessage("Rider ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithMessage("Reason must not exceed 500 characters");
    }
}
