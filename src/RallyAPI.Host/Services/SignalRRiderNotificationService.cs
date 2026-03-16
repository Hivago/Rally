using Microsoft.AspNetCore.SignalR;
using RallyAPI.Host.Hubs;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Host.Services;

/// <summary>
/// Implements IRiderNotificationService using SignalR.
/// Registered in Program.cs to override StubRiderNotificationService from Delivery.Infrastructure.
/// </summary>
public sealed class SignalRRiderNotificationService : IRiderNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ConnectionTracker _tracker;

    public SignalRRiderNotificationService(
        IHubContext<NotificationHub> hubContext,
        ConnectionTracker tracker)
    {
        _hubContext = hubContext;
        _tracker = tracker;
    }

    /// <summary>
    /// Pushes a delivery offer to the rider's group.
    /// Always returns Success — the orchestrator handles timeouts if rider is offline.
    /// </summary>
    public async Task<Result> SendDeliveryOfferAsync(
        Guid riderId,
        DeliveryOfferNotification offer,
        CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group($"rider_{riderId}")
            .SendAsync("ReceiveDeliveryOffer", offer, ct);

        return Result.Success();
    }

    public async Task<Result> SendOfferCancelledAsync(
        Guid riderId,
        Guid offerId,
        string reason,
        CancellationToken ct = default)
    {
        if (!_tracker.IsConnected(riderId))
            return Result.Failure(Error.NotFound("Rider", riderId));

        await _hubContext.Clients
            .Group($"rider_{riderId}")
            .SendAsync("OfferCancelled", new { offerId, reason }, ct);

        return Result.Success();
    }

    public async Task<Result> SendNotificationAsync(
        Guid riderId,
        string title,
        string message,
        CancellationToken ct = default)
    {
        if (!_tracker.IsConnected(riderId))
            return Result.Failure(Error.NotFound("Rider", riderId));

        await _hubContext.Clients
            .Group($"rider_{riderId}")
            .SendAsync("Notification", new { title, message }, ct);

        return Result.Success();
    }

    public Task<bool> IsRiderConnectedAsync(Guid riderId, CancellationToken ct = default) =>
        Task.FromResult(_tracker.IsConnected(riderId));
}
