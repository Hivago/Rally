using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.EscalateOrder;

public sealed class EscalateOrderCommandValidator : AbstractValidator<EscalateOrderCommand>
{
    public EscalateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escalation reason is required.")
            .MaximumLength(500).WithMessage("Escalation reason must be 500 characters or fewer.");
    }
}
