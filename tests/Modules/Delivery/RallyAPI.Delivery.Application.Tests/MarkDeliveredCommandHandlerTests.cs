using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.Commands.MarkDelivered;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Riders;

namespace RallyAPI.Delivery.Application.Tests;

public class MarkDeliveredCommandHandlerTests
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<MarkDeliveredCommandHandler> _logger;
    private readonly MarkDeliveredCommandHandler _handler;

    public MarkDeliveredCommandHandlerTests()
    {
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _riderCommandService = Substitute.For<IRiderCommandService>();
        _logger = Substitute.For<ILogger<MarkDeliveredCommandHandler>>();
        _handler = new MarkDeliveredCommandHandler(_repository, _riderCommandService, _logger);
    }

    [Fact]
    public async Task Handle_WhenDropCodeMatches_ShouldTransitionToDeliveredAndClearRider()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildArrivedAtDrop(riderId);

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkDeliveredCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = riderId,
            DropCode = delivery.DropCode!
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(DeliveryRequestStatus.Delivered);
        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
        await _riderCommandService.Received(1)
            .ClearRiderDeliveryAsync(riderId, delivery.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDropCodeIsWrong_ShouldFailAndNotTransition()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildArrivedAtDrop(riderId);
        var wrongCode = delivery.DropCode == "0000" ? "1111" : "0000";

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkDeliveredCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = riderId,
            DropCode = wrongCode
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Invalid delivery code");
        delivery.Status.Should().Be(DeliveryRequestStatus.RiderArrivedDrop);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _riderCommandService.DidNotReceive()
            .ClearRiderDeliveryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRiderNotAssigned_ShouldFailBeforeCodeCheck()
    {
        var assignedRider = Guid.NewGuid();
        var otherRider = Guid.NewGuid();
        var delivery = BuildArrivedAtDrop(assignedRider);

        _repository.GetByIdAsync(delivery.Id, Arg.Any<CancellationToken>()).Returns(delivery);

        var command = new MarkDeliveredCommand
        {
            DeliveryRequestId = delivery.Id,
            RiderId = otherRider,
            DropCode = delivery.DropCode!
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("not assigned");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    private static DeliveryRequest BuildArrivedAtDrop(Guid riderId)
    {
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-002",
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
        request.MarkPickedUp();
        request.MarkRiderEnRouteDrop();
        request.MarkRiderArrivedDrop();
        return request;
    }
}
