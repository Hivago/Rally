namespace RallyAPI.Orders.Application.Abstractions;

public interface IOpsAlertNotifier
{
    Task NotifyOrderEscalatedAsync(
        Guid orderId,
        string orderNumber,
        Guid restaurantId,
        string restaurantName,
        string? restaurantPhone,
        string reason,
        DateTime escalatedAt,
        CancellationToken cancellationToken = default);
}
