using FluentValidation;

namespace RallyAPI.Users.Application.Restaurants.Commands.VerifyOtp;

public sealed class VerifyRestaurantOtpCommandValidator : AbstractValidator<VerifyRestaurantOtpCommand>
{
    public VerifyRestaurantOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("OTP is required.");
    }
}
