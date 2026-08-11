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

        // Bank details are mandatory at owner creation — a Payout can only ever be reconciled
        // if the owner's bank details existed before their first payout was created (see
        // WeeklyPayoutBatchService / GenerateRestaurantPayoutExportCommandHandler). Letting an
        // owner exist without them is exactly the gap that left real payouts permanently stuck
        // in Processing with no matchable bank details.
        RuleFor(x => x.BankAccountNumber)
            .NotEmpty().WithMessage("Bank account number is required.")
            .Matches(@"^\d{9,18}$").WithMessage("Bank account number must be 9-18 digits.");

        RuleFor(x => x.BankIfscCode)
            .NotEmpty().WithMessage("IFSC code is required.")
            .Length(11).WithMessage("IFSC code must be exactly 11 characters.");

        RuleFor(x => x.BankAccountName)
            .NotEmpty().WithMessage("Bank account holder name is required.")
            .MaximumLength(255);
    }
}
