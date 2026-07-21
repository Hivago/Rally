using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.ResetOwnerPassword;

public sealed class ResetOwnerPasswordCommandValidator
    : AbstractValidator<ResetOwnerPasswordCommand>
{
    public ResetOwnerPasswordCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId).NotEmpty();
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}
