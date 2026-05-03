namespace RallyAPI.SharedKernel.Abstractions.Orders;

/// <summary>
/// Cross-module service for per-rider order/delivery counts.
/// Implemented in Orders.Infrastructure, consumed by Users.Application (admin rider overview).
/// </summary>
public interface IRiderOrderStatsService
{
    Task<RiderDeliveryStats> GetDeliveryStatsAsync(
        Guid riderId,
        CancellationToken cancellationToken = default);
}

public sealed record RiderDeliveryStats(
    int Total,
    int Completed,
    int Cancelled,
    int Ongoing);
