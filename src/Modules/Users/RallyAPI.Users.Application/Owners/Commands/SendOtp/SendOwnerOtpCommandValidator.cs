using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.SendOtp;

public sealed class SendOwnerOtpCommandValidator : AbstractValidator<SendOwnerOtpCommand>
{
    public SendOwnerOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.");
    }
}
