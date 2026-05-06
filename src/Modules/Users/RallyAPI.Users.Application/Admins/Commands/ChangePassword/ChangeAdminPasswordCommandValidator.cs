using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.ChangePassword;

public sealed class ChangeAdminPasswordCommandValidator : AbstractValidator<ChangeAdminPasswordCommand>
{
    public ChangeAdminPasswordCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID is required.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.NewPassword).WithMessage("Confirm password does not match new password.");
    }
}
