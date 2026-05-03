using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.UpdateRiderKyc;

public sealed class UpdateRiderKycCommandValidator : AbstractValidator<UpdateRiderKycCommand>
{
    public UpdateRiderKycCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId)
            .NotEmpty().WithMessage("Requesting admin ID is required.");

        RuleFor(x => x.RiderId)
            .NotEmpty().WithMessage("Rider ID is required.");

        RuleFor(x => x.NewKycStatus)
            .IsInEnum().WithMessage("Invalid KYC status.");
    }
}
