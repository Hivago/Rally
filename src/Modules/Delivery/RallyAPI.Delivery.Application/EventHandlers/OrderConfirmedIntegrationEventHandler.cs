using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Handles OrderConfirmedIntegrationEvent by creating the DeliveryRequest.
///
/// With <see cref="DispatchOptions.EarlyDispatchEnabled"/> OFF (default): the request is created
/// in PendingDispatch with DispatchAt = ConfirmedAt and quotedPrice 0; the food-ready event drives
/// dispatch (today's behavior).
///
/// With early dispatch ON and a quote present: we resolve the real delivery fee from the quote and
/// schedule a predictive DispatchAt = ConfirmedAt + (prep − buffer) so the recovery service's
/// due-dispatch sweep starts the rider search DURING prep. Food-ready remains the floor.
/// </summary>
public sealed class OrderConfirmedIntegrationEventHandler
    : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly IDeliveryQuoteRepository _quoteRepository;
    private readonly PrepTimeCalculator _prepTimeCalculator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DispatchOptions _dispatchOptions;
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger;

    public OrderConfirmedIntegrationEventHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        IDeliveryQuoteRepository quoteRepository,
        PrepTimeCalculator prepTimeCalculator,
        IUnitOfWork unitOfWork,
        IOptions<DispatchOptions> dispatchOptions,
        ILogger<OrderConfirmedIntegrationEventHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _quoteRepository = quoteRepository;
        _prepTimeCalculator = prepTimeCalculator;
        _unitOfWork = unitOfWork;
        _dispatchOptions = dispatchOptions.Value;
        _logger = logger;
    }

    public async Task Handle(
        OrderConfirmedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📦 Processing OrderConfirmedIntegrationEvent for Order {OrderNumber}",
            notification.OrderNumber);

        // ═══════════════════════════════════════════════════════════════
        // PICKUP ORDERS — No delivery needed
        // ═══════════════════════════════════════════════════════════════

        if (notification.IsPickupOrder)
        {
            _logger.LogInformation(
                "🏪 Order {OrderNumber} is a pickup order — skipping delivery request creation.",
                notification.OrderNumber);
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // IDEMPOTENCY CHECK - Prevent duplicate DeliveryRequests
        // ═══════════════════════════════════════════════════════════════

        var existingDelivery = await _deliveryRequestRepository
            .GetByOrderIdAsync(notification.OrderId, cancellationToken);

        if (existingDelivery is not null)
        {
            _logger.LogWarning(
                "⏭️ DeliveryRequest {DeliveryId} already exists for Order {OrderNumber}. " +
                "Skipping duplicate event.",
                existingDelivery.Id,
                notification.OrderNumber);
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // RESOLVE FEE + PREDICTIVE DISPATCH TIME
        // Early dispatch: if enabled and a quote exists, price the delivery from the
        // quote's FinalFee and schedule DispatchAt during prep so the search starts
        // before food-ready. Otherwise fall back to today's behavior (fee 0, dispatch
        // at ConfirmedAt, driven by the food-ready event).
        // ═══════════════════════════════════════════════════════════════

        var quotedPrice = 0m;              // fallback: admin/ready-path handles pricing
        var dispatchAt = notification.ConfirmedAt;
        decimal? distanceKm = null;
        int? estimatedMinutes = null;
        DeliveryQuote? quoteToConsume = null;

        if (_dispatchOptions.EarlyDispatchEnabled && notification.QuoteId is Guid quoteId)
        {
            var quote = await _quoteRepository.GetByIdAsync(quoteId, cancellationToken);

            if (quote is null)
            {
                _logger.LogWarning(
                    "Early dispatch: quote {QuoteId} not found for Order {OrderNumber}; " +
                    "falling back to ready-time dispatch.",
                    quoteId, notification.OrderNumber);
            }
            else
            {
                var prep = _prepTimeCalculator.Calculate(notification.ItemCount);
                quotedPrice = quote.FinalFee;
                dispatchAt = notification.ConfirmedAt.AddMinutes(prep.DispatchAfterMinutes);
                distanceKm = quote.DistanceKm;
                estimatedMinutes = quote.EstimatedMinutes;
                quoteToConsume = quote;

                _logger.LogInformation(
                    "🕒 Early dispatch scheduled for Order {OrderNumber}: fee ₹{Fee}, prep {Prep}min, " +
                    "DispatchAt {DispatchAt:o} ({Offset}min after accept).",
                    notification.OrderNumber, quotedPrice, prep.TotalPrepMinutes,
                    dispatchAt, prep.DispatchAfterMinutes);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATE DELIVERY REQUEST — Status: PendingDispatch
        // ═══════════════════════════════════════════════════════════════

        var deliveryRequest = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: notification.OrderId,
            orderNumber: notification.OrderNumber,
            quoteId: notification.QuoteId,
            quotedPrice: quotedPrice,
            // Pickup (Restaurant)
            pickupLat: notification.PickupLatitude,
            pickupLng: notification.PickupLongitude,
            pickupPincode: notification.PickupPincode,
            pickupAddress: notification.PickupAddress,
            pickupContactName: notification.RestaurantName,
            pickupContactPhone: notification.RestaurantPhone,
            // Drop (Customer)
            dropLat: notification.DropLatitude,
            dropLng: notification.DropLongitude,
            dropPincode: notification.DropPincode,
            dropAddress: notification.DropAddress,
            dropContactName: notification.CustomerName,
            dropContactPhone: notification.CustomerPhone,
            // Order context
            restaurantId: notification.RestaurantId,
            customerId: notification.CustomerId,
            itemCount: notification.ItemCount,
            totalAmount: notification.TotalAmount,
            deliveryInstructions: notification.DeliveryInstructions,
            dispatchAt: dispatchAt,
            distanceKm: distanceKm,
            estimatedMinutes: estimatedMinutes);

        await _deliveryRequestRepository.AddAsync(deliveryRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Consume the quote so it can't be reused — only when we actually priced from it.
        // Done after the request is persisted: if this write fails the delivery still exists.
        if (quoteToConsume is not null)
        {
            quoteToConsume.MarkAsUsed(notification.OrderId);
            await _quoteRepository.UpdateAsync(quoteToConsume, cancellationToken);
        }

        _logger.LogInformation(
            "✅ DeliveryRequest {DeliveryId} created for Order {OrderNumber}. " +
            "Status: PendingDispatch, DispatchAt {DispatchAt:o}.",
            deliveryRequest.Id,
            notification.OrderNumber,
            dispatchAt);

        // No inline dispatch here. Dispatch fires at whichever comes first:
        // the scheduled DispatchAt (recovery service due-sweep, early-dispatch only)
        // or the food-ready event.
    }
}