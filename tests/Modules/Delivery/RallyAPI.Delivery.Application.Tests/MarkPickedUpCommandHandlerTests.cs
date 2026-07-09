using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.Commands.MarkPickedUp;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.Tests;

public class MarkPickedUpCommandHandlerTests
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly ILogger<MarkPickedUpCommandHandler> _logger;
    private readonly MarkPickedUpCommandHandler _handler;

    public MarkPickedUpCommandHandlerTests()
    {
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _logger = Substitute.For<ILogger<MarkPickedUpCommandHandler>>();
        _handler = new MarkPickedUpCommandHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_WhenPickupCodeMatches_ShouldTransitionToPickedUp()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildArrivedAtPickup(riderId);

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = riderId,
            PickupCode = delivery.PickupCode!
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(DeliveryRequestStatus.PickedUp);
        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPickupCodeIsWrong_ShouldFailAndNotTransition()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildArrivedAtPickup(riderId);
        var wrongCode = delivery.PickupCode == "0000" ? "1111" : "0000";

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = riderId,
            PickupCode = wrongCode
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Invalid pickup code");
        delivery.Status.Should().Be(DeliveryRequestStatus.RiderArrivedPickup);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPickupCodeIsNull_ShouldFailCleanlyNotThrow()
    {
        // A null/missing code must be rejected as an invalid code, not blow up
        // with a NullReferenceException (500). Validation normally catches this,
        // but the handler must be null-safe on its own.
        var riderId = Guid.NewGuid();
        var delivery = BuildArrivedAtPickup(riderId);

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = riderId,
            PickupCode = null!
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Invalid pickup code");
        delivery.Status.Should().Be(DeliveryRequestStatus.RiderArrivedPickup);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRiderNotAssigned_ShouldFailBeforeCodeCheck()
    {
        var assignedRider = Guid.NewGuid();
        var otherRider = Guid.NewGuid();
        var delivery = BuildArrivedAtPickup(assignedRider);

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = otherRider,
            PickupCode = delivery.PickupCode!
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("not assigned");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeliveryNotFound_ShouldFail()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DeliveryRequest?)null);

        var command = new MarkPickedUpCommand
        {
            DeliveryRequestId = Guid.NewGuid(),
            RiderId = Guid.NewGuid(),
            PickupCode = "1234"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static DeliveryRequest BuildArrivedAtPickup(Guid riderId)
    {
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-001",
            quoteId: null,
            quotedPrice: 100m,
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            pickupAddress: "Restaurant Street", pickupContactName: "Dosa Corner",
            pickupContactPhone: "+919876543210",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            dropAddress: "42 Brigade Road", dropContactName: "Priya Singh",
            dropContactPhone: "+919845678901");

        request.StartSearchingOwnFleet();
        request.AssignOwnFleetRider(riderId, "Ravi Kumar", "+919812345678");
        request.MarkRiderEnRoutePickup();
        request.MarkRiderArrivedPickup();
        return request;
    }
}
