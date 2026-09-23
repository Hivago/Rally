using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.EventHandlers;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Tests;

public class OrderCancelledIntegrationEventHandlerTests
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<OrderCancelledIntegrationEventHandler> _logger;
    private readonly OrderCancelledIntegrationEventHandler _handler;

    public OrderCancelledIntegrationEventHandlerTests()
    {
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _riderCommandService = Substitute.For<IRiderCommandService>();
        _logger = Substitute.For<ILogger<OrderCancelledIntegrationEventHandler>>();
        _handler = new OrderCancelledIntegrationEventHandler(_repository, _riderCommandService, _logger);

        _riderCommandService
            .ClearRiderDeliveryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
    }

    [Fact]
    public async Task Handle_WhenNoDeliveryRequestExists_DoesNothing()
    {
        var orderId = Guid.NewGuid();
        _repository.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((DeliveryRequest?)null);

        var evt = new OrderCancelledIntegrationEvent(orderId, "ORD-001", "RestaurantUnavailable");

        await _handler.Handle(evt, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _riderCommandService.DidNotReceive().ClearRiderDeliveryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRiderAssignedButNotYetPickedUp_CancelsDeliveryAndReleasesRider()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildAssignedDelivery(riderId);
        _repository.GetByOrderIdAsync(delivery.OrderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new OrderCancelledIntegrationEvent(delivery.OrderId, delivery.OrderNumber, "RestaurantUnavailable");

        await _handler.Handle(evt, CancellationToken.None);

        delivery.Status.Should().Be(DeliveryRequestStatus.Cancelled);
        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
        await _riderCommandService.Received(1).ClearRiderDeliveryAsync(
            riderId, delivery.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoRiderAssignedYet_CancelsDeliveryWithoutTouchingRider()
    {
        var delivery = DeliveryRequest.Create(
            id: Guid.NewGuid(), orderId: Guid.NewGuid(), orderNumber: "ORD-002",
            quoteId: null, quotedPrice: 100m,
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            pickupAddress: "Restaurant Street", pickupContactName: "Dosa Corner", pickupContactPhone: "+919876543210",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            dropAddress: "42 Brigade Road", dropContactName: "Priya Singh", dropContactPhone: "+919845678901");

        _repository.GetByOrderIdAsync(delivery.OrderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new OrderCancelledIntegrationEvent(delivery.OrderId, delivery.OrderNumber, "PaymentTimeout");

        await _handler.Handle(evt, CancellationToken.None);

        delivery.Status.Should().Be(DeliveryRequestStatus.Cancelled);
        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
        await _riderCommandService.DidNotReceive().ClearRiderDeliveryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyDelivered_DoesNothing()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildAssignedDelivery(riderId);
        delivery.MarkRiderEnRoutePickup();
        delivery.MarkRiderArrivedPickup();
        delivery.MarkPickedUp();
        delivery.MarkRiderEnRouteDrop();
        delivery.MarkDelivered();

        _repository.GetByOrderIdAsync(delivery.OrderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new OrderCancelledIntegrationEvent(delivery.OrderId, delivery.OrderNumber, "CustomerRequested");

        await _handler.Handle(evt, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _riderCommandService.DidNotReceive().ClearRiderDeliveryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyPickedUp_ReleasesRiderButLeavesDeliveryStatusForManualResolution()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildAssignedDelivery(riderId);
        delivery.MarkRiderEnRoutePickup();
        delivery.MarkRiderArrivedPickup();
        delivery.MarkPickedUp();

        _repository.GetByOrderIdAsync(delivery.OrderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new OrderCancelledIntegrationEvent(delivery.OrderId, delivery.OrderNumber, "SystemError");

        await _handler.Handle(evt, CancellationToken.None);

        delivery.Status.Should().Be(DeliveryRequestStatus.PickedUp);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _riderCommandService.Received(1).ClearRiderDeliveryAsync(
            riderId, delivery.Id, Arg.Any<CancellationToken>());
    }

    private static DeliveryRequest BuildAssignedDelivery(Guid riderId)
    {
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(), orderId: Guid.NewGuid(), orderNumber: "ORD-CANCEL-001",
            quoteId: null, quotedPrice: 100m,
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            pickupAddress: "Restaurant Street", pickupContactName: "Dosa Corner", pickupContactPhone: "+919876543210",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            dropAddress: "42 Brigade Road", dropContactName: "Priya Singh", dropContactPhone: "+919845678901");

        request.StartSearchingOwnFleet();
        request.AssignOwnFleetRider(riderId, "Ravi Kumar", "+919812345678");
        return request;
    }
}
