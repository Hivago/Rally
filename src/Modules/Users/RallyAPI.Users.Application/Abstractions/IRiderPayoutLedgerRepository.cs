using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderPayoutLedgerRepository
{
    Task<RiderPayoutLedger?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RiderPayoutLedger?> GetByCycleAsync(
        Guid riderId,
        DateTime cycleStartUtc,
        DateTime cycleEndUtc,
        CancellationToken ct = default);

    Task<RiderEarningsBreakdown> GetEarningsBreakdownAsync(
        Guid riderId,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task AddAsync(RiderPayoutLedger payout, CancellationToken ct = default);

    void Update(RiderPayoutLedger payout);
}

public sealed record RiderEarningsBreakdown(
    decimal Total,
    decimal Pending,
    decimal ThisWeek,
    decimal ThisMonth);
