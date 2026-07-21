using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.GenerateRiderPayoutExport;

public sealed class GenerateRiderPayoutExportCommandValidator
    : AbstractValidator<GenerateRiderPayoutExportCommand>
{
    public GenerateRiderPayoutExportCommandValidator()
    {
        RuleFor(x => x.CycleStartUtc).NotEmpty();
        RuleFor(x => x.CycleEndUtc)
            .NotEmpty()
            .GreaterThan(x => x.CycleStartUtc)
            .WithMessage("Cycle end must be after cycle start.");
        RuleFor(x => x.RequestedByAdminId).NotEmpty();
    }
}
