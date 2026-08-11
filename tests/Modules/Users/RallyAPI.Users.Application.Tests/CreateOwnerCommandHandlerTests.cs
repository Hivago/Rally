using FluentAssertions;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.CreateOwner;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class CreateOwnerCommandHandlerTests
{
    private readonly IAdminRepository _adminRepository = Substitute.For<IAdminRepository>();
    private readonly IRestaurantOwnerRepository _ownerRepository = Substitute.For<IRestaurantOwnerRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateOwnerCommandHandler _handler;

    private static readonly Guid AdminId = Guid.NewGuid();

    public CreateOwnerCommandHandlerTests()
    {
        _handler = new CreateOwnerCommandHandler(_adminRepository, _ownerRepository, _passwordHasher, _unitOfWork);

        var admin = Admin.Create(Email.Create("admin@example.com").Value, "hash", "Admin", AdminRole.SuperAdmin).Value;
        _adminRepository.GetByIdAsync(AdminId, Arg.Any<CancellationToken>()).Returns(admin);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
    }

    private static CreateOwnerCommand ValidCommand() =>
        new(
            RequestedByAdminId: AdminId,
            Name: "Ravi Sharma",
            Email: "ravi@example.com",
            Phone: "9876543210",
            Password: "password123",
            PanNumber: null,
            GstNumber: null,
            BankAccountNumber: "123456789012",
            BankIfscCode: "icic0001234",
            BankAccountName: "Ravi Sharma");

    [Fact]
    public async Task Handle_ValidCommand_CreatesOwnerWithBankDetailsStamped()
    {
        _ownerRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        RestaurantOwner? saved = null;
        await _ownerRepository.AddAsync(
            Arg.Do<RestaurantOwner>(o => saved = o), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.BankAccountNumber.Should().Be("123456789012");
        saved.BankIfscCode.Should().Be("ICIC0001234"); // normalized to uppercase
        saved.BankAccountName.Should().Be("Ravi Sharma");
    }

    [Fact]
    public async Task Handle_MalformedIfscFromDomainGuard_ReturnsFailure_DoesNotSaveOwner()
    {
        // Belt-and-suspenders: even if the FluentValidation IFSC-length rule were ever bypassed,
        // RestaurantOwner.UpdateBankDetails() still refuses a malformed IFSC at the domain layer.
        _ownerRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var command = ValidCommand() with { BankIfscCode = "TOOSHORT" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _ownerRepository.DidNotReceive().AddAsync(Arg.Any<RestaurantOwner>(), Arg.Any<CancellationToken>());
    }
}
