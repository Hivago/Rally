using FluentAssertions;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Auth.Commands.RefreshToken;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class RefreshTokenCommandHandlerOwnerTests
{
    private readonly IRestaurantRepository _restaurantRepository = Substitute.For<IRestaurantRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IRestaurantOwnerRepository _ownerRepository = Substitute.For<IRestaurantOwnerRepository>();
    private readonly IJwtProvider _jwtProvider = Substitute.For<IJwtProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerOwnerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepository,
            Substitute.For<ICustomerRepository>(),
            Substitute.For<IRiderRepository>(),
            _restaurantRepository,
            Substitute.For<IAdminRepository>(),
            _ownerRepository,
            _jwtProvider,
            _unitOfWork);

        _jwtProvider.GenerateOwnerTokenPair(Arg.Any<RestaurantOwner>())
            .Returns(new TokenPair("new-access", "new-refresh", DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(60)));
    }

    private static RestaurantOwner BuildOwner() =>
        RestaurantOwner.Create(
            "Balchandra",
            Email.Create($"{Guid.NewGuid()}@example.com").Value,
            "hash",
            PhoneNumber.Create("9998887777").Value).Value;

    private RefreshToken StoreOwnerToken(Guid ownerId)
    {
        var stored = RefreshToken.Create("hash", ownerId, "owner", RefreshToken.DefaultLifetime);
        _refreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(stored);
        return stored;
    }

    [Fact]
    public async Task Handle_OwnerRefreshToken_ShouldIssueOwnerTokenPairAndRotate()
    {
        var owner = BuildOwner();
        var stored = StoreOwnerToken(owner.Id);
        _ownerRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        stored.IsRevoked.Should().BeTrue();
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.UserType == "owner" && t.UserId == owner.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnerDeactivated_ShouldFail()
    {
        var owner = BuildOwner();
        owner.Deactivate();
        StoreOwnerToken(owner.Id);
        _ownerRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OwnerNotFound_ShouldFail()
    {
        StoreOwnerToken(Guid.NewGuid());
        _ownerRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RestaurantOwner?)null);

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static Restaurant BuildRestaurant(Guid? ownerId)
    {
        var restaurant = Restaurant.Create(
            name: "Kalp",
            phone: PhoneNumber.Create("9876543210").Value,
            email: Email.Create($"{Guid.NewGuid()}@example.com").Value,
            passwordHash: "hash",
            addressLine: "42 Brigade Road",
            latitude: 12.9716m,
            longitude: 77.5946m).Value;

        if (ownerId.HasValue)
            restaurant.SetOwner(ownerId.Value);

        return restaurant;
    }

    private RefreshToken StoreOwnerOutletToken(Guid outletId)
    {
        var stored = RefreshToken.Create("hash", outletId, RefreshToken.OwnerOutletUserType, RefreshToken.DefaultLifetime);
        _refreshTokenRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(stored);
        return stored;
    }

    [Fact]
    public async Task Handle_OwnerOutletToken_ShouldKeepOwnerAccessAndSessionType()
    {
        var owner = BuildOwner();
        var restaurant = BuildRestaurant(owner.Id);
        StoreOwnerOutletToken(restaurant.Id);
        _restaurantRepository.GetByIdAsync(restaurant.Id, Arg.Any<CancellationToken>()).Returns(restaurant);
        _restaurantRepository.GetSiblingOutletIdsAsync(restaurant, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { restaurant.Id });
        _ownerRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        _jwtProvider.GenerateOwnerOutletTokenPair(restaurant, owner.Id, Arg.Any<IReadOnlyList<Guid>>())
            .Returns(new TokenPair("outlet-access", "outlet-refresh", DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(60)));

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("outlet-access");
        _jwtProvider.DidNotReceive().GenerateRestaurantTokenPair(Arg.Any<Restaurant>(), Arg.Any<IReadOnlyList<Guid>>());
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.UserType == RefreshToken.OwnerOutletUserType && t.UserId == restaurant.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnerOutletToken_OwnerDeactivated_ShouldFail()
    {
        var owner = BuildOwner();
        owner.Deactivate();
        var restaurant = BuildRestaurant(owner.Id);
        StoreOwnerOutletToken(restaurant.Id);
        _restaurantRepository.GetByIdAsync(restaurant.Id, Arg.Any<CancellationToken>()).Returns(restaurant);
        _ownerRepository.GetByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OwnerOutletToken_RestaurantHasNoOwner_ShouldFail()
    {
        var restaurant = BuildRestaurant(ownerId: null);
        StoreOwnerOutletToken(restaurant.Id);
        _restaurantRepository.GetByIdAsync(restaurant.Id, Arg.Any<CancellationToken>()).Returns(restaurant);

        var result = await _handler.Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
