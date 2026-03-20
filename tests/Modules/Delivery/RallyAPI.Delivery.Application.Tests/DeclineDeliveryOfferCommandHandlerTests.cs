using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using NSubstitute;
using RallyAPI.Delivery.Application.Commands.DeclineDeliveryOffer;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Application.Tests;

public class DeclineDeliveryOfferCommandHandlerTests
{
    private readonly IDeliveryRequestRepository _repository;
    private readonly ILogger<DeclineDeliveryOfferCommandHandler> _logger;
    private readonly DeclineDeliveryOfferCommandHandler _handler;

    public DeclineDeliveryOfferCommandHandlerTests()
    {
        _repository = Substitute.For<IDeliveryRequestRepository>();
        _logger = Substitute.For<ILogger<DeclineDeliveryOfferCommandHandler>>();
        _handler = new DeclineDeliveryOfferCommandHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_WhenOfferNotFound_ShouldReturnFailure()
    {
        var command = new DeclineDeliveryOfferCommand { OfferId = Guid.NewGuid(), RiderId = Guid.NewGuid(), Reason = null };

        _repository.GetByStatusAsync(DeliveryRequestStatus.SearchingOwnFleet, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DeliveryRequest>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenOfferBelongsToDifferentRider_ShouldReturnFailure()
    {
        var riderId = Guid.NewGuid();
        var differentRiderId = Guid.NewGuid();
        var deliveryRequest = BuildSearchingRequest();
        var offer = deliveryRequest.CreateOffer(riderId, earnings: 80m, expiresInSeconds: 60);

        _repository.GetByStatusAsync(DeliveryRequestStatus.SearchingOwnFleet, Arg.Any<CancellationToken>())
            .Returns(new[] { deliveryRequest });
        _repository.GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(deliveryRequest);

        var command = new DeclineDeliveryOfferCommand { OfferId = offer.Id, RiderId = differentRiderId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("not sent to you");
    }

    [Fact]
    public async Task Handle_WhenOfferAlreadyResponded_ShouldReturnFailure()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildSearchingRequest();
        var offer = deliveryRequest.CreateOffer(riderId, earnings: 80m, expiresInSeconds: 60);
        offer.Expire(); // Status is now Expired — not Pending

        _repository.GetByStatusAsync(DeliveryRequestStatus.SearchingOwnFleet, Arg.Any<CancellationToken>())
            .Returns(new[] { deliveryRequest });
        _repository.GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(deliveryRequest);

        var command = new DeclineDeliveryOfferCommand { OfferId = offer.Id, RiderId = riderId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOfferIsExpired_ShouldReturnFailure()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildSearchingRequest();
        // Negative expiresInSeconds → ExpiresAt is in the past, Status stays Pending
        var offer = deliveryRequest.CreateOffer(riderId, earnings: 80m, expiresInSeconds: -100);

        _repository.GetByStatusAsync(DeliveryRequestStatus.SearchingOwnFleet, Arg.Any<CancellationToken>())
            .Returns(new[] { deliveryRequest });
        _repository.GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(deliveryRequest);

        var command = new DeclineDeliveryOfferCommand { OfferId = offer.Id, RiderId = riderId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOfferIsValidAndPending_ShouldDeclineAndPersist()
    {
        var riderId = Guid.NewGuid();
        var deliveryRequest = BuildSearchingRequest();
        var offer = deliveryRequest.CreateOffer(riderId, earnings: 80m, expiresInSeconds: 60);

        _repository.GetByStatusAsync(DeliveryRequestStatus.SearchingOwnFleet, Arg.Any<CancellationToken>())
            .Returns(new[] { deliveryRequest });
        _repository.GetByIdWithOffersAsync(deliveryRequest.Id, Arg.Any<CancellationToken>())
            .Returns(deliveryRequest);

        var command = new DeclineDeliveryOfferCommand { OfferId = offer.Id, RiderId = riderId, Reason = "Too far" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(RiderOfferStatus.Rejected);
        offer.RejectionReason.Should().Be("Too far");
        await _repository.Received(1).UpdateAsync(deliveryRequest, Arg.Any<CancellationToken>());
    }

    #region Helpers

    private static DeliveryRequest BuildSearchingRequest()
    {
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-001",
            quoteId: null,
            quotedPrice: 100m,
            pickupLat: 12.935,
            pickupLng: 77.624,
            pickupPincode: "560095",
            pickupAddress: "Restaurant Street",
            pickupContactName: "Dosa Corner",
            pickupContactPhone: "+919876543210",
            dropLat: 12.971,
            dropLng: 77.594,
            dropPincode: "560025",
            dropAddress: "42 Brigade Road",
            dropContactName: "Priya Singh",
            dropContactPhone: "+919845678901");

        request.StartSearchingOwnFleet();
        return request;
    }

    #endregion
}
