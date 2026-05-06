using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RallyAPI.Host.Hubs;
using RallyAPI.Users.Domain.Events;

namespace RallyAPI.Host.Notifications;

/// <summary>
/// Pushes rider lifecycle events to the admin SignalR group as a unified
/// "AdminRiderFeed" channel. Drives the dashboard counters
/// (onlineRiders, pendingKyc) in the admin panel.
///
/// KycSubmitted carries showToast=true so the admin panel surfaces a toast
/// even when the operator is on a different page — they should not miss a
/// new KYC submission.
/// </summary>
public sealed class AdminRiderFeedHandler :
    INotificationHandler<RiderWentOnlineEvent>,
    INotificationHandler<RiderWentOfflineEvent>,
    INotificationHandler<RiderKycSubmittedEvent>
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<AdminRiderFeedHandler> _logger;

    public AdminRiderFeedHandler(
        IHubContext<NotificationHub> hub,
        ILogger<AdminRiderFeedHandler> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task Handle(RiderWentOnlineEvent notification, CancellationToken ct) =>
        Push("RiderOnline", new
        {
            riderId    = notification.RiderId,
            name       = notification.Name,
            showToast  = false,
            occurredAt = notification.OccurredAt
        }, ct);

    public Task Handle(RiderWentOfflineEvent notification, CancellationToken ct) =>
        Push("RiderOffline", new
        {
            riderId    = notification.RiderId,
            name       = notification.Name,
            showToast  = false,
            occurredAt = notification.OccurredAt
        }, ct);

    public Task Handle(RiderKycSubmittedEvent notification, CancellationToken ct) =>
        Push("KycSubmitted", new
        {
            riderId      = notification.RiderId,
            name         = notification.Name,
            documentType = notification.DocumentType.ToString(),
            showToast    = true,
            occurredAt   = notification.OccurredAt
        }, ct);

    private Task Push(string eventName, object payload, CancellationToken ct)
    {
        var envelope = new
        {
            @event = eventName,
            payload
        };

        return _hub.Clients.Group("admin").SendAsync("AdminRiderFeed", envelope, ct);
    }
}
