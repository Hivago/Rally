using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.AssignRider;

public sealed class AssignRiderCommandValidator : AbstractValidator<AssignRiderCommand>
{
    public AssignRiderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        // This command only ever assigns an own-fleet rider, who must have a real Rally id.
        // 3PL agents reach an order through the Delivery module's assignment event instead.
        RuleFor(x => x.RiderId).NotEmpty();

        RuleFor(x => x.RiderName).MaximumLength(200).When(x => x.RiderName != null);
        RuleFor(x => x.RiderPhone).MaximumLength(20).When(x => x.RiderPhone != null);
    }
}
