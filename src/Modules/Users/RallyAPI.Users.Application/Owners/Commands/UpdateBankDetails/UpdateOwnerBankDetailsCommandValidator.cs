using FluentValidation;

namespace RallyAPI.Users.Application.Owners.Commands.UpdateBankDetails;

public sealed class UpdateOwnerBankDetailsCommandValidator
    : AbstractValidator<UpdateOwnerBankDetailsCommand>
{
    public UpdateOwnerBankDetailsCommandValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("Owner ID is required.");

        RuleFor(x => x.BankAccountNumber)
            .NotEmpty().WithMessage("Bank account number is required.")
            .MaximumLength(20).WithMessage("Bank account number is too long.");

        RuleFor(x => x.BankIfscCode)
            .NotEmpty().WithMessage("IFSC code is required.")
            .Length(11).WithMessage("IFSC code must be exactly 11 characters.");

        RuleFor(x => x.BankAccountName)
            .NotEmpty().WithMessage("Account holder name is required.")
            .MaximumLength(255).WithMessage("Account holder name is too long.");
    }
}
