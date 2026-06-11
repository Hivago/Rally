using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.EventHandlers;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.IntegrationEvents.Riders;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Application.Tests;

public class RiderLocationUpdatedIntegrationEventHandlerTests
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly ICustomerNotificationService _customerNotifications;
    private readonly ILogger<RiderLocationUpdatedIntegrationEventHandler> _logger;
    private readonly RiderLocationUpdatedIntegrationEventHandler _handler;

    public RiderLocationUpdatedIntegrationEventHandlerTests()
    {
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _customerNotifications = Substitute.For<ICustomerNotificationService>();
        _logger = Substitute.For<ILogger<RiderLocationUpdatedIntegrationEventHandler>>();
        _handler = new RiderLocationUpdatedIntegrationEventHandler(
            _repository, _customerNotifications, _logger);

        _customerNotifications
            .SendRiderLocationAsync(Arg.Any<Guid>(), Arg.Any<RiderLocationUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
    }

    [Fact]
    public async Task Handle_WhenRiderHasActiveDelivery_PersistsAndPushesToCustomer()
    {
        var riderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var delivery = BuildActiveDelivery(riderId, customerId);

        _repository.GetActiveByRiderAsync(riderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new RiderLocationUpdatedIntegrationEvent(
            riderId, 12.9352, 77.6245, new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));

        await _handler.Handle(evt, CancellationToken.None);

        delivery.LastRiderLatitude.Should().Be(12.9352);
        delivery.LastRiderLongitude.Should().Be(77.6245);
        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
        await _customerNotifications.Received(1).SendRiderLocationAsync(
            customerId,
            Arg.Is<RiderLocationUpdate>(u =>
                u.OrderId == delivery.OrderId &&
                u.DeliveryRequestId == delivery.Id &&
                u.Latitude == 12.9352 &&
                u.Longitude == 77.6245),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoActiveDelivery_DoesNothing()
    {
        var riderId = Guid.NewGuid();
        _repository.GetActiveByRiderAsync(riderId, Arg.Any<CancellationToken>())
            .Returns((DeliveryRequest?)null);

        var evt = new RiderLocationUpdatedIntegrationEvent(riderId, 1.0, 2.0, DateTime.UtcNow);

        await _handler.Handle(evt, CancellationToken.None);

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _customerNotifications.DidNotReceive().SendRiderLocationAsync(
            Arg.Any<Guid>(), Arg.Any<RiderLocationUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActiveDeliveryHasNoCustomer_PersistsButDoesNotPush()
    {
        var riderId = Guid.NewGuid();
        var delivery = BuildActiveDelivery(riderId, customerId: null);

        _repository.GetActiveByRiderAsync(riderId, Arg.Any<CancellationToken>()).Returns(delivery);

        var evt = new RiderLocationUpdatedIntegrationEvent(riderId, 5.0, 6.0, DateTime.UtcNow);

        await _handler.Handle(evt, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(delivery, Arg.Any<CancellationToken>());
        await _customerNotifications.DidNotReceive().SendRiderLocationAsync(
            Arg.Any<Guid>(), Arg.Any<RiderLocationUpdate>(), Arg.Any<CancellationToken>());
    }

    private static DeliveryRequest BuildActiveDelivery(Guid riderId, Guid? customerId)
    {
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-LOC-001",
            quoteId: null,
            quotedPrice: 100m,
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            pickupAddress: "Restaurant Street", pickupContactName: "Dosa Corner",
            pickupContactPhone: "+919876543210",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            dropAddress: "42 Brigade Road", dropContactName: "Priya Singh",
            dropContactPhone: "+919845678901",
            customerId: customerId);

        request.StartSearchingOwnFleet();
        request.AssignOwnFleetRider(riderId, "Ravi Kumar", "+919812345678");
        return request;
    }
}
