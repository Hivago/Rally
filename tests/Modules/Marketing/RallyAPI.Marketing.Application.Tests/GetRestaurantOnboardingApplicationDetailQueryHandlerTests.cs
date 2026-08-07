using FluentAssertions;
using NSubstitute;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.GetApplicationDetail;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.SharedKernel.Security;
using Xunit;

namespace RallyAPI.Marketing.Application.Tests;

public class GetRestaurantOnboardingApplicationDetailQueryHandlerTests
{
    private readonly IRestaurantOnboardingApplicationRepository _repository = Substitute.For<IRestaurantOnboardingApplicationRepository>();
    private readonly IFieldEncryptionService _encryption = Substitute.For<IFieldEncryptionService>();
    private readonly GetRestaurantOnboardingApplicationDetailQueryHandler _handler;

    public GetRestaurantOnboardingApplicationDetailQueryHandlerTests()
    {
        _handler = new GetRestaurantOnboardingApplicationDetailQueryHandler(_repository, _encryption);
        _encryption.Decrypt(Arg.Any<string>()).Returns(ci => ci.Arg<string>().Replace("ENC:", ""));
    }

    private static RestaurantOnboardingApplication ValidApplication() =>
        RestaurantOnboardingApplication.Create(
            "Sharma Foods", "Ravi Sharma", "9876543210", "ravi@example.com", "Pune", "123 MG Road",
            null, null,
            bankAccountNumberEncrypted: "ENC:123456789012", bankAccountLast4: "9012",
            bankIfscCode: "ICIC0001234", bankAccountName: "Ravi Sharma",
            panNumberEncrypted: "ENC:ABCDE1234F", panLast4: "234F",
            gstNumberEncrypted: "ENC:27ABCDE1234F1Z5", gstLast4: "1Z5",
            source: null, ipAddress: null).Value;

    [Fact]
    public async Task Handle_NotSuperAdmin_ReturnsMaskedFields_NeverDecrypts()
    {
        var app = ValidApplication();
        _repository.GetByIdAsync(app.Id, Arg.Any<CancellationToken>()).Returns(app);

        var result = await _handler.Handle(
            new GetRestaurantOnboardingApplicationDetailQuery(app.Id, IncludeSensitiveDetails: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BankAccountNumber.Should().BeNull();
        result.Value.PanNumber.Should().BeNull();
        result.Value.GstNumber.Should().BeNull();
        result.Value.BankAccountMasked.Should().Be("•••• 9012");
        _encryption.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_SuperAdmin_ReturnsDecryptedFields()
    {
        var app = ValidApplication();
        _repository.GetByIdAsync(app.Id, Arg.Any<CancellationToken>()).Returns(app);

        var result = await _handler.Handle(
            new GetRestaurantOnboardingApplicationDetailQuery(app.Id, IncludeSensitiveDetails: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BankAccountNumber.Should().Be("123456789012");
        result.Value.PanNumber.Should().Be("ABCDE1234F");
        result.Value.GstNumber.Should().Be("27ABCDE1234F1Z5");
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ReturnsNullValue_NotFailure()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RestaurantOnboardingApplication?)null);

        var result = await _handler.Handle(
            new GetRestaurantOnboardingApplicationDetailQuery(Guid.NewGuid(), true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
