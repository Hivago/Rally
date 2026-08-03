using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Entities;

/// <summary>
/// Weekly batch settlement per restaurant owner.
/// Aggregates all PayoutLedger entries for a given period.
/// </summary>
public sealed class Payout : AggregateRoot
{
    public Guid OwnerId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public int OrderCount { get; private set; }
    public decimal GrossOrderAmount { get; private set; }
    public decimal TotalGstCollected { get; private set; }
    public decimal TotalCommission { get; private set; }
    public decimal TotalCommissionGst { get; private set; }
    public decimal TotalTds { get; private set; }
    public decimal NetPayoutAmount { get; private set; }
    public PayoutStatus Status { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankIfscCode { get; private set; }
    public string? TransactionReference { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Batch this payout was included in when last exported to the ICICI bulk-transfer file.</summary>
    public Guid? ExportBatchId { get; private set; }
    public DateTime? ExportedAtUtc { get; private set; }

    // EF Core
    private Payout() { }

    /// <summary>
    /// Creates a payout batch from a collection of ledger entries.
    /// </summary>
    public static Payout CreateFromLedger(
        Guid ownerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<PayoutLedger> ledgerEntries,
        string? bankAccountNumber,
        string? bankIfscCode)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID is required.", nameof(ownerId));

        if (!ledgerEntries.Any())
            throw new ArgumentException("At least one ledger entry is required.", nameof(ledgerEntries));

        return new Payout
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            OrderCount = ledgerEntries.Count,
            GrossOrderAmount = ledgerEntries.Sum(e => e.OrderAmount),
            TotalGstCollected = ledgerEntries.Sum(e => e.GstAmount),
            TotalCommission = ledgerEntries.Sum(e => e.CommissionAmount),
            TotalCommissionGst = ledgerEntries.Sum(e => e.CommissionGst),
            TotalTds = ledgerEntries.Sum(e => e.TdsAmount),
            NetPayoutAmount = ledgerEntries.Sum(e => e.NetAmount),
            Status = PayoutStatus.Pending,
            BankAccountNumber = bankAccountNumber,
            BankIfscCode = bankIfscCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks this payout as included in an exported ICICI bulk-transfer file. Only a Pending
    /// payout can be exported — Processing/OnHold/Paid/Failed payouts are never re-included
    /// in a later export, which is what makes double-export (and so double-pay) impossible.
    /// </summary>
    public void MarkProcessing(Guid exportBatchId)
    {
        if (Status != PayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot process payout in {Status} status.");

        if (exportBatchId == Guid.Empty)
            throw new ArgumentException("Export batch ID is required.", nameof(exportBatchId));

        Status = PayoutStatus.Processing;
        ExportBatchId = exportBatchId;
        ExportedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void MarkPaid(string transactionReference)
    {
        if (Status != PayoutStatus.Processing)
            throw new InvalidOperationException($"Cannot mark as paid from {Status} status.");

        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("Transaction reference is required.", nameof(transactionReference));

        Status = PayoutStatus.Paid;
        TransactionReference = transactionReference.Trim();
        PaidAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void MarkFailed(string? notes = null)
    {
        if (Status != PayoutStatus.Processing)
            throw new InvalidOperationException($"Cannot mark as failed from {Status} status.");

        Status = PayoutStatus.Failed;
        Notes = notes?.Trim();
        MarkAsUpdated();
    }

    public void AddNotes(string notes)
    {
        Notes = notes?.Trim();
        MarkAsUpdated();
    }

    /// <summary>
    /// Admin pause: takes a Pending payout out of the auto-run queue.
    /// Call <see cref="ReleaseHold"/> to bring it back.
    /// </summary>
    public void PutOnHold(string? reason = null)
    {
        if (Status != PayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot put on hold from {Status} status. Only Pending payouts can be paused.");

        Status = PayoutStatus.OnHold;
        if (!string.IsNullOrWhiteSpace(reason))
            Notes = reason.Trim();
        MarkAsUpdated();
    }

    /// <summary>
    /// Admin release: returns an OnHold payout to Pending so the next auto-run picks it up.
    /// </summary>
    public void ReleaseHold()
    {
        if (Status != PayoutStatus.OnHold)
            throw new InvalidOperationException($"Cannot release hold from {Status} status.");

        Status = PayoutStatus.Pending;
        MarkAsUpdated();
    }

    /// <summary>
    /// Admin retry: moves a Failed payout back to Pending and clears the failure note.
    /// </summary>
    public void MarkRetry()
    {
        if (Status != PayoutStatus.Failed)
            throw new InvalidOperationException($"Cannot retry from {Status} status. Only Failed payouts can be retried.");

        Status = PayoutStatus.Pending;
        Notes = null;
        MarkAsUpdated();
    }
}
