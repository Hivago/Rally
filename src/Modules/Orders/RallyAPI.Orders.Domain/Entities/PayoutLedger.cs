using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Entities;

/// <summary>
/// One row per delivered order. Source of truth for all restaurant payout calculations.
/// Tracks GST (5% on order), commission, commission GST (18%), and TDS (1%).
/// </summary>
public sealed class PayoutLedger : BaseEntity
{
    public Guid OwnerId { get; private set; }
    public Guid OutletId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal OrderAmount { get; private set; }
    public decimal GstAmount { get; private set; }
    public decimal CommissionPercentage { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal CommissionGst { get; private set; }
    public decimal TdsAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; } = "INR";
    public PayoutLedgerStatus Status { get; private set; }
    public Guid? PayoutId { get; private set; }

    // EF Core
    private PayoutLedger() { }

    /// <summary>
    /// Creates a payout ledger entry with all financial calculations.
    /// Called when an order is delivered.
    /// </summary>
    public static PayoutLedger Create(
        Guid ownerId,
        Guid outletId,
        Guid orderId,
        decimal orderAmount,
        decimal commissionPercentage)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID is required.", nameof(ownerId));

        if (outletId == Guid.Empty)
            throw new ArgumentException("Outlet ID is required.", nameof(outletId));

        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID is required.", nameof(orderId));

        if (orderAmount <= 0)
            throw new ArgumentException("Order amount must be positive.", nameof(orderAmount));

        // Calculate financial breakdown per Indian tax law
        var gstAmount = Math.Round(orderAmount * 0.05m, 2);              // 5% GST on food (Section 9(5) CGST)
        var commissionAmount = Math.Round(orderAmount * commissionPercentage / 100m, 2);
        var commissionGst = Math.Round(commissionAmount * 0.18m, 2);     // 18% GST on commission (service)
        var netBeforeTds = orderAmount - commissionAmount;
        var tdsAmount = Math.Round(netBeforeTds * 0.01m, 2);             // 1% TDS (Section 194-O)
        var netAmount = netBeforeTds - tdsAmount;

        return new PayoutLedger
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            OutletId = outletId,
            OrderId = orderId,
            OrderAmount = orderAmount,
            GstAmount = gstAmount,
            CommissionPercentage = commissionPercentage,
            CommissionAmount = commissionAmount,
            CommissionGst = commissionGst,
            TdsAmount = tdsAmount,
            NetAmount = netAmount,
            Currency = "INR",
            Status = PayoutLedgerStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void AssignToPayout(Guid payoutId)
    {
        if (Status != PayoutLedgerStatus.Pending)
            throw new InvalidOperationException($"Ledger entry is already in {Status} status.");

        PayoutId = payoutId;
        Status = PayoutLedgerStatus.Batched;
        MarkAsUpdated();
    }

    public void MarkAsPaidOut()
    {
        if (Status != PayoutLedgerStatus.Batched)
            throw new InvalidOperationException($"Ledger entry must be batched before marking as paid out.");

        Status = PayoutLedgerStatus.PaidOut;
        MarkAsUpdated();
    }
}
