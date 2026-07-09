using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RallyAPI.Delivery.Application.EventHandlers;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;
using Xunit;

namespace RallyAPI.Delivery.Application.Tests;

public class OrderConfirmedEarlyDispatchTests
{
    private readonly IDeliveryRequestRepository _requestRepository = Substitute.For<IDeliveryRequestRepository>();
    private readonly IDeliveryQuoteRepository _quoteRepository = Substitute.For<IDeliveryQuoteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger =
        Substitute.For<ILogger<OrderConfirmedIntegrationEventHandler>>();

    private static readonly DateTime ConfirmedAt = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);

    private OrderConfirmedIntegrationEventHandler BuildHandler(bool earlyDispatchEnabled)
    {
        var dispatchOptions = Options.Create(new DispatchOptions { EarlyDispatchEnabled = earlyDispatchEnabled });
        var prep = new PrepTimeCalculator(Options.Create(new PrepTimeOptions())); // base 15, buffer 5
        return new OrderConfirmedIntegrationEventHandler(
            _requestRepository, _quoteRepository, prep, _unitOfWork, dispatchOptions, _logger);
    }

    [Fact]
    public async Task Handle_WhenEarlyDispatchOnWithQuote_PricesFromQuoteAndSchedulesPredictiveDispatch()
    {
        var quoteId = Guid.NewGuid();
        var quote = BuildQuote(finalFee: 45m, distanceKm: 3m, estimatedMinutes: 20);
        _quoteRepository.GetByIdAsync(quoteId, Arg.Any<CancellationToken>()).Returns(quote);

        DeliveryRequest? captured = null;
        await _requestRepository.AddAsync(
            Arg.Do<DeliveryRequest>(r => captured = r), Arg.Any<CancellationToken>());

        var handler = BuildHandler(earlyDispatchEnabled: true);
        // 1 item → prep 15, dispatchAfter = 15 - 5 = 10 min
        await handler.Handle(BuildEvent(quoteId, itemCount: 1), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(DeliveryRequestStatus.PendingDispatch);
        captured.QuotedPrice.Should().Be(45m);
        captured.DispatchAt.Should().Be(ConfirmedAt.AddMinutes(10));
        captured.DistanceKm.Should().Be(3m);
        captured.EstimatedMinutes.Should().Be(20);

        quote.IsUsed.Should().BeTrue();
        await _quoteRepository.Received(1).UpdateAsync(quote, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEarlyDispatchOff_UsesTodaysBehaviorAndDoesNotTouchQuote()
    {
        var quoteId = Guid.NewGuid();

        DeliveryRequest? captured = null;
        await _requestRepository.AddAsync(
            Arg.Do<DeliveryRequest>(r => captured = r), Arg.Any<CancellationToken>());

        var handler = BuildHandler(earlyDispatchEnabled: false);
        await handler.Handle(BuildEvent(quoteId, itemCount: 1), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(DeliveryRequestStatus.PendingDispatch);
        captured.QuotedPrice.Should().Be(0m);
        captured.DispatchAt.Should().Be(ConfirmedAt);

        await _quoteRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _quoteRepository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryQuote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEarlyDispatchOnButNoQuoteId_FallsBackToReadyTimeDispatch()
    {
        DeliveryRequest? captured = null;
        await _requestRepository.AddAsync(
            Arg.Do<DeliveryRequest>(r => captured = r), Arg.Any<CancellationToken>());

        var handler = BuildHandler(earlyDispatchEnabled: true);
        await handler.Handle(BuildEvent(quoteId: null, itemCount: 1), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.QuotedPrice.Should().Be(0m);
        captured.DispatchAt.Should().Be(ConfirmedAt);
        await _quoteRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEarlyDispatchOnButQuoteMissing_FallsBackWithoutConsuming()
    {
        var quoteId = Guid.NewGuid();
        _quoteRepository.GetByIdAsync(quoteId, Arg.Any<CancellationToken>()).Returns((DeliveryQuote?)null);

        DeliveryRequest? captured = null;
        await _requestRepository.AddAsync(
            Arg.Do<DeliveryRequest>(r => captured = r), Arg.Any<CancellationToken>());

        var handler = BuildHandler(earlyDispatchEnabled: true);
        await handler.Handle(BuildEvent(quoteId, itemCount: 1), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.QuotedPrice.Should().Be(0m);
        captured.DispatchAt.Should().Be(ConfirmedAt);
        await _quoteRepository.DidNotReceive().UpdateAsync(Arg.Any<DeliveryQuote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeliveryAlreadyExists_SkipsCreation()
    {
        var evt = BuildEvent(Guid.NewGuid(), itemCount: 1);
        _requestRepository.GetByOrderIdAsync(evt.OrderId, Arg.Any<CancellationToken>())
            .Returns(BuildExistingRequest());

        var handler = BuildHandler(earlyDispatchEnabled: true);
        await handler.Handle(evt, CancellationToken.None);

        await _requestRepository.DidNotReceive().AddAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
        await _quoteRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPickupOrder_DoesNotCreateDeliveryRequest()
    {
        var handler = BuildHandler(earlyDispatchEnabled: true);
        await handler.Handle(BuildEvent(Guid.NewGuid(), itemCount: 1, isPickupOrder: true), CancellationToken.None);

        await _requestRepository.DidNotReceive().AddAsync(Arg.Any<DeliveryRequest>(), Arg.Any<CancellationToken>());
    }

    #region Helpers

    private static DeliveryQuote BuildQuote(decimal finalFee, decimal distanceKm, int estimatedMinutes) =>
        DeliveryQuote.CreateOwnFleet(
            id: Guid.NewGuid(),
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            city: "Bengaluru",
            orderAmount: 500m,
            restaurantId: Guid.NewGuid(),
            distanceKm: distanceKm,
            baseFee: finalFee,
            finalFee: finalFee,
            estimatedMinutes: estimatedMinutes,
            expiresAt: DateTime.UtcNow.AddMinutes(30));

    private static OrderConfirmedIntegrationEvent BuildEvent(
        Guid? quoteId, int itemCount, bool isPickupOrder = false) =>
        new(
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-001",
            restaurantId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            restaurantName: "Dosa Corner",
            restaurantPhone: "+919876543210",
            pickupAddress: "Restaurant Street",
            pickupLatitude: 12.935,
            pickupLongitude: 77.624,
            pickupPincode: "560095",
            customerName: "Priya Singh",
            customerPhone: "+919845678901",
            dropAddress: "42 Brigade Road",
            dropLatitude: 12.971,
            dropLongitude: 77.594,
            dropPincode: "560025",
            itemCount: itemCount,
            totalAmount: 500m,
            deliveryInstructions: null,
            quoteId: quoteId,
            confirmedAt: ConfirmedAt,
            isPickupOrder: isPickupOrder);

    private static DeliveryRequest BuildExistingRequest() =>
        DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "ORD-001",
            quoteId: null,
            quotedPrice: 100m,
            pickupLat: 12.935, pickupLng: 77.624, pickupPincode: "560095",
            pickupAddress: "Restaurant Street", pickupContactName: "Dosa Corner", pickupContactPhone: "+919876543210",
            dropLat: 12.971, dropLng: 77.594, dropPincode: "560025",
            dropAddress: "42 Brigade Road", dropContactName: "Priya Singh", dropContactPhone: "+919845678901");

    #endregion
}
