using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Domain.Repositories;

public interface IPayoutRepository
{
    Task<Payout?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Payout payout, CancellationToken ct = default);
    void Update(Payout payout);

    Task<IReadOnlyList<Payout>> GetByOwnerIdAsync(
        Guid ownerId, int skip = 0, int take = 20, CancellationToken ct = default);

    Task<IReadOnlyList<Payout>> GetByStatusAsync(
        PayoutStatus status, int skip = 0, int take = 50, CancellationToken ct = default);

    Task<Payout?> GetCurrentPeriodPayoutAsync(
        Guid ownerId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default);
}
