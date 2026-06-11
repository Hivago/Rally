using RallyAPI.SharedKernel.Results;

namespace RallyAPI.SharedKernel.Abstractions.Notifications;

/// <summary>
/// Service for sending real-time notifications to customers.
/// MVP implementation uses SignalR (pushes to the customer_{id} group).
/// </summary>
public interface ICustomerNotificationService
{
    /// <summary>
    /// Pushes a live rider GPS position to the customer tracking their order.
    /// </summary>
    /// <param name="customerId">Target customer ID</param>
    /// <param name="update">Rider location payload</param>
    /// <param name="ct">Cancellation token</param>
    Task<Result> SendRiderLocationAsync(
        Guid customerId,
        RiderLocationUpdate update,
        CancellationToken ct = default);
}

/// <summary>
/// Live rider position pushed to the customer during an active delivery.
/// </summary>
public sealed record RiderLocationUpdate(
    Guid OrderId,
    Guid DeliveryRequestId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);
