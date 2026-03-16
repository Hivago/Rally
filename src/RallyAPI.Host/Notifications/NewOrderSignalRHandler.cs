using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Notifies the restaurant's SignalR group when an order is confirmed.
/// Pushes "NewOrderReceived" so the restaurant dashboard can show an incoming order alert.
/// </summary>
public sealed class NewOrderSignalRHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IOrderRepository _orders;
    private readonly ILogger<NewOrderSignalRHandler> _logger;

    public NewOrderSignalRHandler(
        IHubContext<NotificationHub> hub,
        IOrderRepository orders,
        ILogger<NewOrderSignalRHandler> logger)
    {
        _hub = hub;
        _orders = orders;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning("NewOrderSignalRHandler: order {OrderId} not found, skipping push", notification.OrderId);
            return;
        }

        await _hub.Clients
            .Group($"restaurant_{notification.RestaurantId}")
            .SendAsync("NewOrderReceived", new
            {
                orderId     = order.Id,
                orderNumber = order.OrderNumber.Value,
                itemCount   = order.Items.Count,
                totalAmount = order.Pricing.Total.Amount
            }, ct);
    }
}
