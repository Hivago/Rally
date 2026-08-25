using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Riders;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Delivery.Application.EventHandlers;

/// <summary>
/// Handles OrderCancelledIntegrationEvent: cancels the DeliveryRequest (if one exists and
/// is still cancellable) and releases any rider pinned to it.
///
/// Without this handler, cancelling an order (e.g. restaurant no-show / pickup timeout,
/// admin force-cancel) left the DeliveryRequest at whatever status it was in and the
/// rider's CurrentDeliveryId permanently set — the rider's app kept showing the dead
/// delivery and they were blocked from receiving any new offers, with no recovery path
/// short of an admin manually running ReleaseRiderDelivery.
///
/// Pickup orders and orders that never reached DeliveryRequest creation (payment/restaurant
/// confirm timeouts) are a no-op here — nothing to cancel.
/// </summary>
public sealed class OrderCancelledIntegrationEventHandler
    : INotificationHandler<OrderCancelledIntegrationEvent>
{
    private readonly IDeliveryRequestRepository _deliveryRequestRepository;
    private readonly IRiderCommandService _riderCommandService;
    private readonly ILogger<OrderCancelledIntegrationEventHandler> _logger;

    public OrderCancelledIntegrationEventHandler(
        IDeliveryRequestRepository deliveryRequestRepository,
        IRiderCommandService riderCommandService,
        ILogger<OrderCancelledIntegrationEventHandler> logger)
    {
        _deliveryRequestRepository = deliveryRequestRepository;
        _riderCommandService = riderCommandService;
        _logger = logger;
    }

    public async Task Handle(
        OrderCancelledIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var deliveryRequest = await _deliveryRequestRepository.GetByOrderIdAsync(
            notification.OrderId, cancellationToken);

        if (deliveryRequest is null)
        {
            _logger.LogInformation(
                "OrderCancelledIntegrationEvent for Order {OrderNumber}: no DeliveryRequest exists, nothing to release.",
                notification.OrderNumber);
            return;
        }

        var riderToRelease = deliveryRequest.RiderId;

        // Already terminal — a previous MarkDelivered/MarkFailed/Cancel already resolved it.
        // Nothing left to do; the rider (if any) was already released by that handler.
        if (deliveryRequest.Status is DeliveryRequestStatus.Delivered
            or DeliveryRequestStatus.Cancelled
            or DeliveryRequestStatus.Failed
            or DeliveryRequestStatus.RtoDelivered
            or DeliveryRequestStatus.RtoDisposed)
        {
            _logger.LogInformation(
                "OrderCancelledIntegrationEvent for Order {OrderNumber}: DeliveryRequest {DeliveryId} already terminal ({Status}), nothing to do.",
                notification.OrderNumber, deliveryRequest.Id, deliveryRequest.Status);
            return;
        }

        // DeliveryRequest.Cancel() throws once the rider has physically picked up the food
        // (Status >= PickedUp) — the food is out in the world, so the order record being
        // cancelled doesn't undo that. Only flip the DeliveryRequest to Cancelled pre-pickup
        // (this is the reported bug's exact case: restaurant timeout, rider never arrived).
        // Past pickup, leave the DeliveryRequest status alone for ops to resolve with the
        // real-world outcome (MarkDelivered/MarkFailed) — but still release the rider below,
        // since either way they should not be blocked from new offers by a dead order record.
        if (deliveryRequest.Status < DeliveryRequestStatus.PickedUp)
        {
            deliveryRequest.Cancel(notification.Reason);
            await _deliveryRequestRepository.UpdateAsync(deliveryRequest, cancellationToken);

            _logger.LogWarning(
                "Order {OrderNumber} cancelled ({Reason}): DeliveryRequest {DeliveryId} cancelled too.",
                notification.OrderNumber, notification.Reason, deliveryRequest.Id);
        }
        else
        {
            _logger.LogWarning(
                "Order {OrderNumber} cancelled ({Reason}) but DeliveryRequest {DeliveryId} is already past pickup ({Status}) — " +
                "leaving its status for manual resolution, only releasing the rider.",
                notification.OrderNumber, notification.Reason, deliveryRequest.Id, deliveryRequest.Status);
        }

        if (riderToRelease is Guid riderId)
        {
            var clearResult = await _riderCommandService.ClearRiderDeliveryAsync(
                riderId, deliveryRequest.Id, cancellationToken);

            if (clearResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to release rider {RiderId} from cancelled delivery {DeliveryId} (Order {OrderNumber}): {Error}. " +
                    "Rider is likely still stuck — needs ReleaseRiderDelivery.",
                    riderId, deliveryRequest.Id, notification.OrderNumber, clearResult.Error.Message);
            }
            else
            {
                _logger.LogInformation(
                    "Released rider {RiderId} from cancelled delivery {DeliveryId} (Order {OrderNumber}).",
                    riderId, deliveryRequest.Id, notification.OrderNumber);
            }
        }
    }
}
