namespace RallyAPI.SharedKernel.Abstractions.Orders;

public interface IDeliveryEarningsQueryService
{
    Task<IReadOnlyList<RiderEarningsSummary>> GetEarningsByCycleAsync(
        DateTimeOffset cycleStart,
        DateTimeOffset cycleEnd,
        CancellationToken ct = default);
}

public sealed record RiderEarningsSummary(
    Guid RiderId,
    int DeliveryCount,
    decimal TotalDeliveryFee);
