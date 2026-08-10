using FluentValidation;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.SubmitApplication;

/// <summary>
/// Format validation runs here, on the PLAINTEXT command fields, before the handler encrypts
/// PAN/GST/bank account number. Once encrypted, format can no longer be validated (ciphertext
/// is opaque) — so this is the only place these shapes are ever checked.
/// </summary>
public sealed class SubmitRestaurantOnboardingApplicationCommandValidator
    : AbstractValidator<SubmitRestaurantOnboardingApplicationCommand>
{
    public SubmitRestaurantOnboardingApplicationCommandValidator()
    {
        RuleFor(x => x.RestaurantName)
            .NotEmpty().WithMessage("Restaurant name is required.")
            .MaximumLength(200);

        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("Owner name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^[6-9]\d{9}$").WithMessage("Phone must be a valid 10-digit Indian mobile number.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(255);

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100);

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500);

        RuleFor(x => x.CuisineType)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.CuisineType));

        RuleFor(x => x.FssaiNumber)
            .Matches(@"^\d{14}$").WithMessage("FSSAI number must be 14 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.FssaiNumber));

        // Temporarily optional — onboarding.hivago.in doesn't collect bank details yet.
        // Still validated when provided; revert to always-required once the form catches up.
        RuleFor(x => x.BankAccountNumber!)
            .Matches(@"^\d{9,18}$").WithMessage("Bank account number must be 9-18 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.BankAccountNumber));

        RuleFor(x => x.BankIfscCode!)
            .Matches(@"^[A-Z]{4}0[A-Z0-9]{6}$").WithMessage("IFSC code is not a valid format (e.g. ICIC0001234).")
            .When(x => !string.IsNullOrWhiteSpace(x.BankIfscCode));

        RuleFor(x => x.BankAccountName!)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.BankAccountName));

        RuleFor(x => x.PanNumber)
            .NotEmpty().WithMessage("PAN number is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$").WithMessage("PAN is not a valid format (e.g. ABCDE1234F).");

        RuleFor(x => x.GstNumber)
            .Matches(@"^\d{2}[A-Z]{5}\d{4}[A-Z]{1}[A-Z\d]{1}Z[A-Z\d]{1}$")
            .WithMessage("GSTIN is not a valid format.")
            .When(x => !string.IsNullOrWhiteSpace(x.GstNumber));

        RuleFor(x => x.Source)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Source));
    }
}
