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

    public void MarkProcessing()
    {
        if (Status != PayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot process payout in {Status} status.");

        Status = PayoutStatus.Processing;
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
}
