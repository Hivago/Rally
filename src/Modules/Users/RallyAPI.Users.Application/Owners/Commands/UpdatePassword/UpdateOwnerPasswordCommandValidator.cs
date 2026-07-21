using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.UpdatePassword;

public sealed class UpdateOwnerPasswordCommandValidator
    : AbstractValidator<UpdateOwnerPasswordCommand>
{
    public UpdateOwnerPasswordCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();

        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128);
    }
}
