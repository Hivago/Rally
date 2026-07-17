using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Handles rider assignment from Delivery module.
/// Updates the Order aggregate with rider details.
/// </summary>
public sealed class DeliveryRiderAssignedEventHandler : INotificationHandler<DeliveryRiderAssignedIntegrationEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryRiderAssignedEventHandler> _logger;

    public DeliveryRiderAssignedEventHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryRiderAssignedEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeliveryRiderAssignedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling DeliveryRiderAssignedIntegrationEvent for Order {OrderId}",
            notification.OrderId);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while handling rider assignment", notification.OrderId);
            return;
        }

        try
        {
            order.AssignRider(
                notification.RiderId,
                notification.IsOwnFleet,
                notification.RiderName,
                notification.RiderPhone,
                notification.TrackingUrl);

            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated Order {OrderNumber} with rider information (ownFleet: {IsOwnFleet})",
                order.OrderNumber.Value, notification.IsOwnFleet);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Never let a rider-assignment hiccup bubble up and fail the whole caller
            // (e.g. a ProRouting webhook or the manual refresh-status reconcile). The
            // order simply keeps its current rider info; reconcile can be retried.
            _logger.LogWarning(ex,
                "Failed to apply rider assignment to Order {OrderId} (ownFleet: {IsOwnFleet}, rider {RiderId}, name '{RiderName}')",
                notification.OrderId, notification.IsOwnFleet, notification.RiderId, notification.RiderName);
        }
    }
}
