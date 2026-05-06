using FluentValidation;

namespace RallyAPI.Users.Application.Admins.Commands.CreateOwner;

public sealed class CreateOwnerCommandValidator : AbstractValidator<CreateOwnerCommand>
{
    public CreateOwnerCommandValidator()
    {
        RuleFor(x => x.RequestedByAdminId)
            .NotEmpty().WithMessage("Requesting admin ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Owner name is required.")
            .MaximumLength(255).WithMessage("Owner name is too long.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        // Optional fields — validated only when provided. Domain enforces exact length.
        RuleFor(x => x.PanNumber!)
            .Length(10).WithMessage("PAN number must be exactly 10 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PanNumber));

        RuleFor(x => x.GstNumber!)
            .Length(15).WithMessage("GST number must be exactly 15 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.GstNumber));

        RuleFor(x => x.BankIfscCode!)
            .Length(11).WithMessage("IFSC code must be exactly 11 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankIfscCode));

        // Bank fields are all-or-nothing: if any one is provided, all three must be.
        When(x =>
            !string.IsNullOrWhiteSpace(x.BankAccountNumber)
            || !string.IsNullOrWhiteSpace(x.BankIfscCode)
            || !string.IsNullOrWhiteSpace(x.BankAccountName), () =>
        {
            RuleFor(x => x.BankAccountNumber).NotEmpty().WithMessage("Bank account number is required when providing bank details.");
            RuleFor(x => x.BankIfscCode).NotEmpty().WithMessage("IFSC code is required when providing bank details.");
            RuleFor(x => x.BankAccountName).NotEmpty().WithMessage("Account holder name is required when providing bank details.");
        });
    }
}
