using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Notifies the admin SignalR group when an order is escalated.
/// Pushes "OrderEscalated" so the admin panel can surface the alert immediately.
/// </summary>
public sealed class EscalationSignalRHandler : INotificationHandler<OrderEscalatedToAdminEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<EscalationSignalRHandler> _logger;

    public EscalationSignalRHandler(
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<EscalationSignalRHandler> logger)
    {
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    public async Task Handle(OrderEscalatedToAdminEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning("EscalationSignalRHandler: order {OrderId} not found, skipping push", notification.OrderId);
            return;
        }

        await _hub.Clients
            .Group("admin")
            .SendAsync("OrderEscalated", new
            {
                orderId        = order.Id,
                orderNumber    = order.OrderNumber.Value,
                restaurantName = order.RestaurantName,
                reason         = notification.EscalationReason,
                escalatedAt    = notification.EscalatedAt
            }, ct);
    }
}
