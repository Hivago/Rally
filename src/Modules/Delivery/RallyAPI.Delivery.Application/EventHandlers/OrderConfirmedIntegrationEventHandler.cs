using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Handles OrderConfirmedIntegrationEvent.
/// Creates a DeliveryRequest and waits for Admin to dispatch manually.
/// NO automatic rider dispatch - Admin will call rider and update status.
/// </summary>
public sealed class OrderConfirmedIntegrationEventHandler
    : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger;

    public OrderConfirmedIntegrationEventHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderConfirmedIntegrationEventHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _unitOfWork = unitOfWork;
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
        // CREATE DELIVERY REQUEST
        // Status: PendingDispatch (waiting for Admin to assign rider)
        // ═══════════════════════════════════════════════════════════════

        var deliveryRequest = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: notification.OrderId,
            orderNumber: notification.OrderNumber,
            quoteId: notification.QuoteId,
            quotedPrice: 0m, // Delivery fee not available here; admin handles pricing
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
            // Status: PendingDispatch (dispatchAt set to now)
            dispatchAt: notification.ConfirmedAt);

        await _deliveryRequestRepository.AddAsync(deliveryRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "✅ DeliveryRequest {DeliveryId} created for Order {OrderNumber}. " +
            "Status: PendingDispatch. Waiting for Admin to assign rider.",
            deliveryRequest.Id,
            notification.OrderNumber);

        // ═══════════════════════════════════════════════════════════════
        // NO AUTO-DISPATCH!
        // Admin will see this in dashboard and manually:
        // 1. Call the rider
        // 2. Share pickup/drop details
        // 3. Update status in system
        // ═══════════════════════════════════════════════════════════════
    }
}