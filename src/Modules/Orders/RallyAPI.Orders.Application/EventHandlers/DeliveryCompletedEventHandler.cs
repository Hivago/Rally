using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Handles delivery completion notification from Delivery module.
/// Updates order status to Delivered.
/// </summary>
public sealed class DeliveryCompletedEventHandler : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryCompletedEventHandler> _logger;

    public DeliveryCompletedEventHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryCompletedEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeliveryCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling DeliveryCompletedIntegrationEvent for Order {OrderId}",
            notification.OrderId);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while handling completion notification", notification.OrderId);
            return;
        }

        try
        {
            order.MarkDelivered();
            
            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated Order {OrderNumber} status to Delivered", order.OrderNumber.Value);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to mark Order {OrderId} as delivered due to invalid state transition", notification.OrderId);
        }
    }
}
