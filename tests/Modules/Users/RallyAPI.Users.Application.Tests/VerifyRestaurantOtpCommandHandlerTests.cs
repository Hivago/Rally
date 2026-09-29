using FluentAssertions;
using NSubstitute;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Restaurants.Commands.VerifyOtp;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;
using Xunit;

namespace RallyAPI.Users.Application.Tests;

public class VerifyRestaurantOtpCommandHandlerTests
{
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();
    private readonly IRestaurantRepository _restaurantRepository = Substitute.For<IRestaurantRepository>();
    private readonly IJwtProvider _jwtProvider = Substitute.For<IJwtProvider>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly VerifyRestaurantOtpCommandHandler _handler;

    public VerifyRestaurantOtpCommandHandlerTests()
    {
        _handler = new VerifyRestaurantOtpCommandHandler(
            _otpService, _restaurantRepository, _jwtProvider, _refreshTokenRepository, _unitOfWork);

        _otpService.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _jwtProvider.GenerateRestaurantTokenPair(Arg.Any<Restaurant>(), Arg.Any<IReadOnlyList<Guid>>())
            .Returns(new TokenPair("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddDays(30)));
    }

    private static Restaurant BuildActiveRestaurant(string name, Guid? ownerId = null)
    {
        var restaurant = Restaurant.Create(
            name: name,
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

    [Fact]
    public async Task Handle_SingleActiveMatchWithNoOwner_ShouldSucceed()
    {
        // The common case: a single-outlet operator with OwnerId == null must still
        // succeed — this must not regress when the same-owner multi-match path is added.
        var restaurant = BuildActiveRestaurant("Kalp");
        _restaurantRepository.GetByPhoneAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(new List<Restaurant> { restaurant });

        var result = await _handler.Handle(
            new VerifyRestaurantOtpCommand("9876543210", "1234"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(restaurant.Id);
    }

    [Fact]
    public async Task Handle_MultipleMatchesSameOwner_ShouldSucceedWithFullOutletSet()
    {
        var ownerId = Guid.NewGuid();
        var kalp = BuildActiveRestaurant("Kalp", ownerId);
        var lordFork = BuildActiveRestaurant("Lord Fork", ownerId);
        _restaurantRepository.GetByPhoneAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(new List<Restaurant> { kalp, lordFork });

        var result = await _handler.Handle(
            new VerifyRestaurantOtpCommand("9876543210", "1234"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _jwtProvider.Received(1).GenerateRestaurantTokenPair(
            Arg.Any<Restaurant>(),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 2 && ids.Contains(kalp.Id) && ids.Contains(lordFork.Id)));
    }

    [Fact]
    public async Task Handle_MultipleMatchesDifferentOwners_ShouldReturnFailure()
    {
        var kalp = BuildActiveRestaurant("Kalp", Guid.NewGuid());
        var unrelated = BuildActiveRestaurant("Unrelated Diner", Guid.NewGuid());
        _restaurantRepository.GetByPhoneAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(new List<Restaurant> { kalp, unrelated });

        var result = await _handler.Handle(
            new VerifyRestaurantOtpCommand("9876543210", "1234"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleMatchesNoSharedOwner_ShouldReturnFailure()
    {
        // Two unrelated, not-yet-linked restaurants sharing a phone with no OwnerId set —
        // genuinely ambiguous, must still reject (spec §4.2 edge-case note).
        var first = BuildActiveRestaurant("Restaurant A");
        var second = BuildActiveRestaurant("Restaurant B");
        _restaurantRepository.GetByPhoneAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(new List<Restaurant> { first, second });

        var result = await _handler.Handle(
            new VerifyRestaurantOtpCommand("9876543210", "1234"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoActiveMatch_ShouldReturnFailure()
    {
        _restaurantRepository.GetByPhoneAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(new List<Restaurant>());

        var result = await _handler.Handle(
            new VerifyRestaurantOtpCommand("9876543210", "1234"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
