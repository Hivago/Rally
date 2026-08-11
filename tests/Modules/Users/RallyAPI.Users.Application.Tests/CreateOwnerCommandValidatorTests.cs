using FluentAssertions;
using RallyAPI.Users.Application.Admins.Commands.CreateOwner;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

/// <summary>
/// Bank details became mandatory at owner creation to close the gap that left real payouts
/// permanently stuck in Processing with no matchable bank details (see
/// GenerateRestaurantPayoutExportCommandHandlerTests.Handle_StampsLiveBankDetailsOntoPayout...
/// in the Orders test suite for the downstream half of that fix).
/// </summary>
public class CreateOwnerCommandValidatorTests
{
    private readonly CreateOwnerCommandValidator _validator = new();

    private static CreateOwnerCommand ValidCommand(
        string bankAccountNumber = "123456789012",
        string bankIfscCode = "ICIC0001234",
        string bankAccountName = "Ravi Sharma") =>
        new(
            RequestedByAdminId: Guid.NewGuid(),
            Name: "Ravi Sharma",
            Email: "ravi@example.com",
            Phone: "9876543210",
            Password: "password123",
            PanNumber: null,
            GstNumber: null,
            BankAccountNumber: bankAccountNumber,
            BankIfscCode: bankIfscCode,
            BankAccountName: bankAccountName);

    [Fact]
    public void Validate_WithValidBankDetails_Succeeds()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyBankAccountNumber_Fails()
    {
        var result = _validator.Validate(ValidCommand(bankAccountNumber: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOwnerCommand.BankAccountNumber));
    }

    [Fact]
    public void Validate_WithEmptyBankIfscCode_Fails()
    {
        var result = _validator.Validate(ValidCommand(bankIfscCode: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOwnerCommand.BankIfscCode));
    }

    [Fact]
    public void Validate_WithEmptyBankAccountName_Fails()
    {
        var result = _validator.Validate(ValidCommand(bankAccountName: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOwnerCommand.BankAccountName));
    }

    [Fact]
    public void Validate_WithNonNumericBankAccountNumber_Fails()
    {
        var result = _validator.Validate(ValidCommand(bankAccountNumber: "not-a-number"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMalformedIfsc_Fails()
    {
        var result = _validator.Validate(ValidCommand(bankIfscCode: "TOO-SHORT"));

        result.IsValid.Should().BeFalse();
    }
}
