using FluentAssertions;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.Marketing.Domain.Enums;
using Xunit;

namespace RallyAPI.Marketing.Application.Tests;

public class RestaurantOnboardingApplicationTests
{
    private static RestaurantOnboardingApplication ValidApplication()
    {
        var result = RestaurantOnboardingApplication.Create(
            restaurantName: "Sharma Foods",
            ownerName: "Ravi Sharma",
            phone: "9876543210",
            email: "ravi@example.com",
            city: "Pune",
            addressLine: "123 MG Road",
            cuisineType: "North Indian",
            fssaiNumber: "12345678901234",
            bankAccountNumberEncrypted: "encrypted-blob-1",
            bankAccountLast4: "3333",
            bankIfscCode: "icic0001234",
            bankAccountName: "Ravi Sharma",
            panNumberEncrypted: "encrypted-blob-2",
            panLast4: "234F",
            gstNumberEncrypted: "encrypted-blob-3",
            gstLast4: "0001",
            source: "partner-with-us-page",
            ipAddress: "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_WithValidData_Succeeds_AndDefaultsToPending()
    {
        var app = ValidApplication();

        app.Status.Should().Be(OnboardingApplicationStatus.Pending);
        app.BankIfscCode.Should().Be("ICIC0001234"); // normalized to uppercase
    }

    [Fact]
    public void Create_MissingRestaurantName_Fails()
    {
        var result = RestaurantOnboardingApplication.Create(
            "", "Ravi Sharma", "9876543210", "ravi@example.com", "Pune", "123 MG Road",
            null, null, "enc1", "3333", "ICIC0001234", "Ravi Sharma", "enc2", "234F",
            null, null, null, null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_MissingBankAccountNumber_Fails()
    {
        var result = RestaurantOnboardingApplication.Create(
            "Sharma Foods", "Ravi Sharma", "9876543210", "ravi@example.com", "Pune", "123 MG Road",
            null, null, bankAccountNumberEncrypted: "", "3333", "ICIC0001234", "Ravi Sharma",
            "enc2", "234F", null, null, null, null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Approve_FromPending_Succeeds()
    {
        var app = ValidApplication();
        var adminId = Guid.NewGuid();

        var result = app.Approve(adminId, "Looks good, documents verified.");

        result.IsSuccess.Should().BeTrue();
        app.Status.Should().Be(OnboardingApplicationStatus.Approved);
        app.ReviewedByAdminId.Should().Be(adminId);
        app.ReviewNotes.Should().Be("Looks good, documents verified.");
    }

    [Fact]
    public void Approve_AlreadyApproved_Fails()
    {
        var app = ValidApplication();
        app.Approve(Guid.NewGuid(), null);

        var result = app.Approve(Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Reject_WithoutReason_Fails()
    {
        var app = ValidApplication();

        var result = app.Reject(Guid.NewGuid(), "");

        result.IsFailure.Should().BeTrue();
        app.Status.Should().Be(OnboardingApplicationStatus.Pending); // unchanged
    }

    [Fact]
    public void Reject_WithReason_Succeeds()
    {
        var app = ValidApplication();

        var result = app.Reject(Guid.NewGuid(), "Bank details could not be verified.");

        result.IsSuccess.Should().BeTrue();
        app.Status.Should().Be(OnboardingApplicationStatus.Rejected);
    }

    [Fact]
    public void Approve_AfterRejected_Fails()
    {
        var app = ValidApplication();
        app.Reject(Guid.NewGuid(), "Not a real restaurant.");

        var result = app.Approve(Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
    }
}
