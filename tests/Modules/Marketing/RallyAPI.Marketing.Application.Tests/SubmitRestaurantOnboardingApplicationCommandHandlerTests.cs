using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.SubmitApplication;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.SharedKernel.Security;
using Xunit;

namespace RallyAPI.Marketing.Application.Tests;

public class SubmitRestaurantOnboardingApplicationCommandHandlerTests
{
    private readonly IRestaurantOnboardingApplicationRepository _repository = Substitute.For<IRestaurantOnboardingApplicationRepository>();
    private readonly IFieldEncryptionService _encryption = Substitute.For<IFieldEncryptionService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IOnboardingNotificationService _notificationService = Substitute.For<IOnboardingNotificationService>();
    private readonly SubmitRestaurantOnboardingApplicationCommandHandler _handler;

    public SubmitRestaurantOnboardingApplicationCommandHandlerTests()
    {
        _handler = new SubmitRestaurantOnboardingApplicationCommandHandler(
            _repository, _encryption, _unitOfWork, _notificationService,
            Substitute.For<ILogger<SubmitRestaurantOnboardingApplicationCommandHandler>>());

        // Identity-ish stub: prefix so we can assert encryption was actually invoked.
        _encryption.Encrypt(Arg.Any<string>()).Returns(ci => "ENC:" + ci.Arg<string>());
    }

    private static SubmitRestaurantOnboardingApplicationCommand ValidCommand(string? gst = "27ABCDE1234F1Z5") =>
        new(
            RestaurantName: "Sharma Foods",
            OwnerName: "Ravi Sharma",
            Phone: "9876543210",
            Email: "ravi@example.com",
            City: "Pune",
            AddressLine: "123 MG Road",
            CuisineType: "North Indian",
            FssaiNumber: "12345678901234",
            BankAccountNumber: "123456789012",
            BankIfscCode: "ICIC0001234",
            BankAccountName: "Ravi Sharma",
            PanNumber: "ABCDE1234F",
            GstNumber: gst,
            Source: "partner-with-us-page",
            IpAddress: "127.0.0.1");

    [Fact]
    public async Task Handle_ValidSubmission_EncryptsSensitiveFields_NeverStoresPlaintext()
    {
        _repository.HasPendingApplicationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        RestaurantOnboardingApplication? saved = null;
        await _repository.AddAsync(
            Arg.Do<RestaurantOnboardingApplication>(a => saved = a), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.BankAccountNumberEncrypted.Should().Be("ENC:123456789012");
        saved.PanNumberEncrypted.Should().Be("ENC:ABCDE1234F");
        saved.GstNumberEncrypted.Should().Be("ENC:27ABCDE1234F1Z5");
        saved.BankAccountLast4.Should().Be("9012");
        saved.PanLast4.Should().Be("234F");

        // The plaintext values must never appear anywhere on the persisted entity.
        saved.BankAccountNumberEncrypted.Should().NotBe("123456789012");
        saved.PanNumberEncrypted.Should().NotBe("ABCDE1234F");
    }

    [Fact]
    public async Task Handle_NoGstProvided_LeavesGstFieldsNull()
    {
        _repository.HasPendingApplicationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        RestaurantOnboardingApplication? saved = null;
        await _repository.AddAsync(
            Arg.Do<RestaurantOnboardingApplication>(a => saved = a), Arg.Any<CancellationToken>());

        await _handler.Handle(ValidCommand(gst: null), CancellationToken.None);

        saved!.GstNumberEncrypted.Should().BeNull();
        saved.GstLast4.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PendingApplicationAlreadyExistsForPhoneOrEmail_ReturnsConflict_DoesNotEncryptOrSave()
    {
        _repository.HasPendingApplicationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _encryption.DidNotReceive().Encrypt(Arg.Any<string>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<RestaurantOnboardingApplication>(), Arg.Any<CancellationToken>());
    }
}
