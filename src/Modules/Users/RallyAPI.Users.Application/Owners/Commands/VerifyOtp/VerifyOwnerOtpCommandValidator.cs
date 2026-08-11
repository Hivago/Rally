using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.VerifyOtp;

public sealed class VerifyOwnerOtpCommandValidator : AbstractValidator<VerifyOwnerOtpCommand>
{
    public VerifyOwnerOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("OTP is required.");
    }
}
