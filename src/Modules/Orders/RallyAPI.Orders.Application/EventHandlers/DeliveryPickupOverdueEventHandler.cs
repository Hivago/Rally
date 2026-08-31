using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.SharedKernel.IntegrationEvents.Delivery;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Handles a late-pickup signal from the Delivery module: a rider is assigned but hasn't
/// picked up within the recovery service's idle threshold. Escalates the order to admin —
/// never auto-cancels, since the assigned rider may still be on their way.
/// </summary>
public sealed class DeliveryPickupOverdueEventHandler : INotificationHandler<DeliveryPickupOverdueIntegrationEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryPickupOverdueEventHandler> _logger;

    public DeliveryPickupOverdueEventHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryPickupOverdueEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeliveryPickupOverdueIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError("Order {OrderId} not found while handling pickup-overdue notification", notification.OrderId);
            return;
        }

        order.EscalateToAdmin(
            $"Rider assigned but has not picked up after {notification.IdleFor.TotalMinutes:F0} minute(s).");

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Escalated Order {OrderNumber} to admin: rider assigned but not picked up for {IdleMinutes:F0} min",
            order.OrderNumber.Value, notification.IdleFor.TotalMinutes);
    }
}
