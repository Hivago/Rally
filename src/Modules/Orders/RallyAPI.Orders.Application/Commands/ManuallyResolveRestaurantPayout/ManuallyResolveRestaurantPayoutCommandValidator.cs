using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.ManuallyResolveRestaurantPayout;

public sealed class ManuallyResolveRestaurantPayoutCommandValidator
    : AbstractValidator<ManuallyResolveRestaurantPayoutCommand>
{
    public ManuallyResolveRestaurantPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
        RuleFor(x => x.ResolvedByAdminId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("Manual overrides require a concrete reason (e.g. how you verified this in the ICICI portal), at least 10 characters.");
        RuleFor(x => x.TransactionReference)
            .NotEmpty()
            .MinimumLength(10)
            .When(x => x.Outcome == ManualPayoutResolutionOutcome.Paid)
            .WithMessage("A bank UTR/transaction reference is required to manually mark a payout Paid.");
    }
}
