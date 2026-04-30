using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.RiderPayoutActions;

public sealed class PayNowRiderPayoutCommandValidator : AbstractValidator<PayNowRiderPayoutCommand>
{
    public PayNowRiderPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
    }
}

public sealed class HoldRiderPayoutCommandValidator : AbstractValidator<HoldRiderPayoutCommand>
{
    public HoldRiderPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ReleaseHoldRiderPayoutCommandValidator : AbstractValidator<ReleaseHoldRiderPayoutCommand>
{
    public ReleaseHoldRiderPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
    }
}

public sealed class RetryRiderPayoutCommandValidator : AbstractValidator<RetryRiderPayoutCommand>
{
    public RetryRiderPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
    }
}
