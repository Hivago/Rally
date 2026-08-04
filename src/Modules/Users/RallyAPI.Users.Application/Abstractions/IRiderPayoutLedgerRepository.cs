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

    /// <summary>
    /// All Pending payouts for an exact cycle, across all riders. Used by the weekly ICICI
    /// export — only Pending rows are ever eligible, which is what makes double-export
    /// impossible.
    /// </summary>
    Task<IReadOnlyList<RiderPayoutLedger>> GetPendingByCycleAsync(
        DateTime cycleStartUtc,
        DateTime cycleEndUtc,
        CancellationToken ct = default);

    /// <summary>
    /// All payouts stamped with the given export batch, regardless of status. Used by
    /// reconciliation to scope the ICICI result-file rows to exactly the payouts that went
    /// out in that batch.
    /// </summary>
    Task<IReadOnlyList<RiderPayoutLedger>> GetByExportBatchIdAsync(
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
    Task<IReadOnlyList<RiderPayoutLedger>> GetStaleProcessingAsync(
        DateTime olderThanUtc, CancellationToken ct = default);

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
