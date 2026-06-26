using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.ReleaseRiderDelivery;

public sealed class ReleaseRiderDeliveryCommandValidator : AbstractValidator<ReleaseRiderDeliveryCommand>
{
    public ReleaseRiderDeliveryCommandValidator()
    {
        RuleFor(x => x.RiderId).NotEmpty();
    }
}
