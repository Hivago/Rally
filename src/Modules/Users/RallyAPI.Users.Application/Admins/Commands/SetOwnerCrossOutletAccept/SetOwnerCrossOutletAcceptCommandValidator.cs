using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.SetOwnerCrossOutletAccept;

public sealed class SetOwnerCrossOutletAcceptCommandValidator
    : AbstractValidator<SetOwnerCrossOutletAcceptCommand>
{
    public SetOwnerCrossOutletAcceptCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId).NotEmpty();
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}
