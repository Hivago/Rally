using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.ResetRestaurantPassword;

public sealed class ResetRestaurantPasswordCommandValidator
    : AbstractValidator<ResetRestaurantPasswordCommand>
{
    public ResetRestaurantPasswordCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId).NotEmpty();
        RuleFor(x => x.RestaurantId).NotEmpty();
    }
}
