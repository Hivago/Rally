using FluentValidation;

namespace RallyAPI.Marketing.Application.Waitlist.Commands.JoinWaitlist;

public sealed class JoinWaitlistCommandValidator : AbstractValidator<JoinWaitlistCommand>
{
    public JoinWaitlistCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(320).WithMessage("Email is too long.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^\d{10}$").WithMessage("Phone must be a 10-digit number.");

        RuleFor(x => x.Source)
            .MaximumLength(100).WithMessage("Source must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));
    }
}
