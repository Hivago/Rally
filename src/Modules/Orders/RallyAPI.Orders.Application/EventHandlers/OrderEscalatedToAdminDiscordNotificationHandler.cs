using MediatR;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Events;

namespace RallyAPI.Orders.Application.EventHandlers;

/// <summary>
/// Bridges OrderEscalatedToAdminEvent to the ops-alerts Discord channel so a stuck order
/// (restaurant not confirming) is visible immediately instead of only in the admin dashboard.
/// </summary>
public sealed class OrderEscalatedToAdminDiscordNotificationHandler : INotificationHandler<OrderEscalatedToAdminEvent>
{
    private readonly IOpsAlertNotifier _notifier;

    public OrderEscalatedToAdminDiscordNotificationHandler(IOpsAlertNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Handle(OrderEscalatedToAdminEvent notification, CancellationToken cancellationToken)
    {
        return _notifier.NotifyOrderEscalatedAsync(
            notification.OrderId,
            notification.OrderNumber,
            notification.RestaurantId,
            notification.RestaurantName,
            notification.RestaurantPhone,
            notification.EscalationReason,
            notification.EscalatedAt,
            cancellationToken);
    }
}
