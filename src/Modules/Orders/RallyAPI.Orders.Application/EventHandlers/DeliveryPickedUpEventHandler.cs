using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Handles order pickup notification from Delivery module.
/// Updates order status to PickedUp.
/// </summary>
public sealed class DeliveryPickedUpEventHandler : INotificationHandler<DeliveryPickedUpIntegrationEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryPickedUpEventHandler> _logger;

    public DeliveryPickedUpEventHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryPickedUpEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeliveryPickedUpIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling DeliveryPickedUpIntegrationEvent for Order {OrderId}",
            notification.OrderId);

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while handling pickup notification", notification.OrderId);
            return;
        }

        try
        {
            order.MarkPickedUp();
            
            _orderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated Order {OrderNumber} status to PickedUp", order.OrderNumber.Value);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to mark Order {OrderId} as picked up due to invalid state transition", notification.OrderId);
        }
    }
}
