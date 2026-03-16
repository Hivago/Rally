using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RallyAPI.Delivery.Application.Commands.AcceptDeliveryOffer;
using System.Security.Claims;

namespace RallyAPI.Host.Hubs;

/// <summary>
/// Single hub for all real-time notifications.
/// Groups: rider_{userId}, customer_{userId}, restaurant_{userId}, admin.
/// JWT is read from query string ?access_token= for WebSocket upgrade compatibility.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IMediator _mediator;
    private readonly ConnectionTracker _tracker;

    public NotificationHub(IMediator mediator, ConnectionTracker tracker)
    {
        _mediator = mediator;
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var userType = Context.User?.FindFirst("user_type")?.Value;

        if (userId.HasValue && userType is not null)
        {
            _tracker.AddConnection(userId.Value, Context.ConnectionId);

            var groupName = userType switch
            {
                "rider"      => $"rider_{userId.Value}",
                "customer"   => $"customer_{userId.Value}",
                "restaurant" => $"restaurant_{userId.Value}",
                "admin"      => "admin",
                _            => null
            };

            if (groupName is not null)
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
            _tracker.RemoveConnection(userId.Value, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by rider app to accept a delivery offer.
    /// Dispatches AcceptDeliveryOfferCommand through the existing handler.
    /// </summary>
    public async Task AcceptDeliveryOffer(Guid offerId)
    {
        var riderId = GetUserId();
        if (!riderId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unable to resolve rider identity.");
            return;
        }

        var result = await _mediator.Send(new AcceptDeliveryOfferCommand
        {
            OfferId = offerId,
            RiderId = riderId.Value
        });

        if (result.IsFailure)
            await Clients.Caller.SendAsync("Error", result.Error.Message);
    }

    /// <summary>
    /// Called by rider app to decline a delivery offer.
    /// TODO: Implement DeclineDeliveryOfferCommand and dispatch it here.
    /// </summary>
    public Task DeclineDeliveryOffer(Guid offerId)
    {
        // DeclineDeliveryOfferCommand does not exist yet — wire up once created.
        return Task.CompletedTask;
    }

    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
