using RallyAPI.SharedKernel.Domain;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Domain.Entities;

/// <summary>
/// Per-rider weekly payout cycle. Aggregated by RiderPayoutAggregationJob from
/// delivered orders in the cycle. Admin can hold / release / retry / pay-now via
/// the methods below — same lifecycle as the restaurant-side Payout aggregate.
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

    public void MarkPaidImmediate(string transactionReference)
    {
        if (Status != RiderPayoutStatus.Pending && Status != RiderPayoutStatus.OnHold)
            throw new InvalidOperationException($"Cannot pay-now from {Status} status.");

        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("Transaction reference is required.", nameof(transactionReference));

        Status = RiderPayoutStatus.Paid;
        TransactionReference = transactionReference.Trim();
        PaidAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
