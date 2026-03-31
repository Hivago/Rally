using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Domain.Repositories;

public interface IPayoutLedgerRepository
{
    Task<PayoutLedger?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PayoutLedger?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(PayoutLedger entry, CancellationToken ct = default);

    Task<IReadOnlyList<PayoutLedger>> GetPendingByOwnerIdAsync(
        Guid ownerId, CancellationToken ct = default);

    Task<IReadOnlyList<PayoutLedger>> GetByPayoutIdAsync(
        Guid payoutId, CancellationToken ct = default);

    Task<IReadOnlyList<PayoutLedger>> GetByOwnerIdAsync(
        Guid ownerId, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Returns all distinct owner IDs that have pending ledger entries
    /// created within the given date range.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOwnerIdsWithPendingEntriesAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Returns ledger entries for an owner within a date range.
    /// </summary>
    Task<IReadOnlyList<PayoutLedger>> GetByOwnerIdAndDateRangeAsync(
        Guid ownerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
