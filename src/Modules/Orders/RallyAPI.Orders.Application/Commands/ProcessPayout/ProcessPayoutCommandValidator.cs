using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.ProcessPayout;

public sealed class ProcessPayoutCommandValidator : AbstractValidator<ProcessPayoutCommand>
{
    public ProcessPayoutCommandValidator()
    {
        RuleFor(x => x.PayoutId).NotEmpty();
        RuleFor(x => x.TransactionReference)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Transaction reference is required.");
    }
}
