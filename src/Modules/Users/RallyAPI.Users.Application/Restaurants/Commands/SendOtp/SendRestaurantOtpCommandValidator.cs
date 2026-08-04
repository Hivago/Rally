using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.SendOtp;

public sealed class SendRestaurantOtpCommandValidator : AbstractValidator<SendRestaurantOtpCommand>
{
    public SendRestaurantOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.");
    }
}
