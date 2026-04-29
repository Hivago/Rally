using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.AdminAssignRider;

public sealed class AdminAssignRiderCommandValidator : AbstractValidator<AdminAssignRiderCommand>
{
    public AdminAssignRiderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.RiderId).NotEmpty().WithMessage("Rider ID is required.");
    }
}
