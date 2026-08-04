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

    /// <summary>
    /// All Pending payouts for an exact period. Used by the weekly ICICI export — only
    /// Pending rows are ever eligible, which is what makes double-export impossible.
    /// </summary>
    Task<IReadOnlyList<Payout>> GetPendingByPeriodAsync(
        DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default);

    /// <summary>
    /// All payouts stamped with the given export batch, regardless of status. Used by
    /// reconciliation to scope the ICICI result-file rows to exactly the payouts that went
    /// out in that batch.
    /// </summary>
    Task<IReadOnlyList<Payout>> GetByExportBatchIdAsync(
        Guid exportBatchId, CancellationToken ct = default);

    /// <summary>
    /// True if any payout already carries this bank-issued transaction reference (UTR).
    /// Used by reconciliation to reject a duplicate/replayed UTR before marking a second
    /// payout Paid off the same real transfer.
    /// </summary>
    Task<bool> ExistsWithTransactionReferenceAsync(
        string transactionReference, CancellationToken ct = default);

    /// <summary>
    /// Payouts still Processing (exported, not yet reconciled) whose ExportedAtUtc is older
    /// than the given cutoff — the "stuck in the bank" report so nothing rots silently past
    /// the point a human should look at it.
    /// </summary>
    Task<IReadOnlyList<Payout>> GetStaleProcessingAsync(
        DateTime olderThanUtc, CancellationToken ct = default);
}
