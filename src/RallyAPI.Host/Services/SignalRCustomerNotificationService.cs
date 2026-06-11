using Microsoft.AspNetCore.SignalR;
using RallyAPI.Host.Hubs;
using RallyAPI.SharedKernel.Abstractions.Notifications;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Host.Services;

/// <summary>
/// Implements ICustomerNotificationService using SignalR.
/// Pushes to the customer_{id} group, mirroring OrderStatusSignalRHandler.
/// Lives in Host to avoid a circular dependency on IHubContext{NotificationHub}.
/// </summary>
public sealed class SignalRCustomerNotificationService : ICustomerNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRCustomerNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task<Result> SendRiderLocationAsync(
        Guid customerId,
        RiderLocationUpdate update,
        CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group($"customer_{customerId}")
            .SendAsync("RiderLocationUpdate", new
            {
                orderId = update.OrderId,
                deliveryRequestId = update.DeliveryRequestId,
                riderLatitude = update.Latitude,
                riderLongitude = update.Longitude,
                updatedAt = update.UpdatedAt
            }, ct);

        return Result.Success();
    }
}
