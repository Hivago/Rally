using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Handles delivery failure notification from Delivery module.
/// Updates order status to Failed and handles error details.
/// </summary>
public sealed class DeliveryFailedEventHandler : INotificationHandler<DeliveryFailedIntegrationEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryFailedEventHandler> _logger;

    public DeliveryFailedEventHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryFailedEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeliveryFailedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling DeliveryFailedIntegrationEvent for Order {OrderId}. Reason: {Reason}",
            notification.OrderId,
            notification.FailureReason);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while handling failure notification", notification.OrderId);
            return;
        }

        order.MarkFailed($"{notification.FailureReason}: {notification.FailureNotes}");
        
        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated Order {OrderNumber} status to Failed", order.OrderNumber.Value);
    }
}
