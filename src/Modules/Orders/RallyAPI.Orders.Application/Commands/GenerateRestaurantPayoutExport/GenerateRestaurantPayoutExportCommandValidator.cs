using FluentValidation;

namespace RallyAPI.Orders.Application.Commands.GenerateRestaurantPayoutExport;

public sealed class GenerateRestaurantPayoutExportCommandValidator
    : AbstractValidator<GenerateRestaurantPayoutExportCommand>
{
    public GenerateRestaurantPayoutExportCommandValidator()
    {
        RuleFor(x => x.PeriodStart).NotEmpty();
        RuleFor(x => x.PeriodEnd)
            .NotEmpty()
            .GreaterThan(x => x.PeriodStart)
            .WithMessage("Period end must be after period start.");
        RuleFor(x => x.RequestedByAdminId).NotEmpty();
    }
}
