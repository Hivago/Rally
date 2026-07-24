using RallyAPI.SharedKernel.Domain;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Domain.Entities;

/// <summary>
/// Per-rider weekly payout cycle. Aggregated by RiderPayoutAggregationJob from
/// delivered orders in the cycle. Admin can hold / release / retry via the methods
/// below; Paid is reached only by reconciling the ICICI bank statement — same
/// lifecycle as the restaurant-side Payout aggregate.
/// </summary>
public sealed class RiderPayoutLedger : AggregateRoot
{
    public Guid RiderId { get; private set; }
    public DateTime CycleStartUtc { get; private set; }
    public DateTime CycleEndUtc { get; private set; }
    public int DeliveryCount { get; private set; }

    /// <summary>Earnings attributable to base delivery fee (pct of order DeliveryFee).</summary>
    public decimal BaseFare { get; private set; }

    /// <summary>Surge component. Placeholder zero until surge pricing ships.</summary>
    public decimal SurgeFare { get; private set; }

    /// <summary>Customer tips. Placeholder zero until tipping ships.</summary>
    public decimal Tips { get; private set; }

    public decimal NetPayable { get; private set; }
    public RiderPayoutStatus Status { get; private set; }
    public string? StatusNote { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string? TransactionReference { get; private set; }

    /// <summary>Batch this payout was included in when last exported to the ICICI bulk-transfer file.</summary>
    public Guid? ExportBatchId { get; private set; }
    public DateTime? ExportedAtUtc { get; private set; }

    // EF Core
    private RiderPayoutLedger() { }

    public static RiderPayoutLedger Create(
        Guid riderId,
        DateTime cycleStartUtc,
        DateTime cycleEndUtc,
        int deliveryCount,
        decimal baseFare,
        decimal surgeFare,
        decimal tips)
    {
        if (riderId == Guid.Empty)
            throw new ArgumentException("Rider ID is required.", nameof(riderId));

        if (cycleStartUtc >= cycleEndUtc)
            throw new ArgumentException("Cycle start must be before cycle end.");

        if (deliveryCount < 0)
            throw new ArgumentException("Delivery count cannot be negative.", nameof(deliveryCount));

        if (baseFare < 0 || surgeFare < 0 || tips < 0)
            throw new ArgumentException("Earnings components cannot be negative.");

        return new RiderPayoutLedger
        {
            Id = Guid.NewGuid(),
            RiderId = riderId,
            CycleStartUtc = cycleStartUtc,
            CycleEndUtc = cycleEndUtc,
            DeliveryCount = deliveryCount,
            BaseFare = baseFare,
            SurgeFare = surgeFare,
            Tips = tips,
            NetPayable = baseFare + surgeFare + tips,
            Status = RiderPayoutStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Used by the aggregation job to refresh amounts on a still-Pending row when more
    /// deliveries land in the same cycle. Skip when the row has been touched by an admin
    /// (OnHold / Paid / Failed).
    /// </summary>
    public void UpdateAmounts(int deliveryCount, decimal baseFare, decimal surgeFare, decimal tips)
    {
        if (Status != RiderPayoutStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot refresh amounts on a {Status} payout — admin action takes precedence.");

        DeliveryCount = deliveryCount;
        BaseFare = baseFare;
        SurgeFare = surgeFare;
        Tips = tips;
        NetPayable = baseFare + surgeFare + tips;
        MarkAsUpdated();
    }

    public void PutOnHold(string? reason = null)
    {
        if (Status != RiderPayoutStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot put on hold from {Status} status. Only Pending payouts can be paused.");

        Status = RiderPayoutStatus.OnHold;
        if (!string.IsNullOrWhiteSpace(reason))
            StatusNote = reason.Trim();
        MarkAsUpdated();
    }

    public void ReleaseHold()
    {
        if (Status != RiderPayoutStatus.OnHold)
            throw new InvalidOperationException($"Cannot release hold from {Status} status.");

        Status = RiderPayoutStatus.Pending;
        MarkAsUpdated();
    }

    public void MarkRetry()
    {
        if (Status != RiderPayoutStatus.Failed)
            throw new InvalidOperationException(
                $"Cannot retry from {Status} status. Only Failed payouts can be retried.");

        Status = RiderPayoutStatus.Pending;
        FailureReason = null;
        MarkAsUpdated();
    }

    public void MarkFailed(string reason)
    {
        if (Status != RiderPayoutStatus.Pending && Status != RiderPayoutStatus.Processing)
            throw new InvalidOperationException($"Cannot mark failed from {Status} status.");

        Status = RiderPayoutStatus.Failed;
        FailureReason = reason?.Trim();
        MarkAsUpdated();
    }

    /// <summary>
    /// Marks this payout as included in an exported ICICI bulk-transfer file. Only a Pending
    /// payout can be exported — Processing/OnHold/Paid/Failed payouts are never re-included
    /// in a later export, which is what makes double-export (and so double-pay) impossible.
    /// </summary>
    public void MarkProcessing(Guid exportBatchId)
    {
        if (Status != RiderPayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot process payout in {Status} status.");

        if (exportBatchId == Guid.Empty)
            throw new ArgumentException("Export batch ID is required.", nameof(exportBatchId));

        Status = RiderPayoutStatus.Processing;
        ExportBatchId = exportBatchId;
        ExportedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>
    /// Marks this payout as Paid after reconciling the ICICI bank statement. Requires the
    /// real bank-issued UTR — this is the only path to Paid (see ReconcileRiderPayoutsCommand).
    /// </summary>
    public void MarkPaid(string transactionReference)
    {
        if (Status != RiderPayoutStatus.Processing)
            throw new InvalidOperationException($"Cannot mark as paid from {Status} status.");

        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("Transaction reference is required.", nameof(transactionReference));

        Status = RiderPayoutStatus.Paid;
        TransactionReference = transactionReference.Trim();
        PaidAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
