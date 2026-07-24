using FluentAssertions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using Xunit;

namespace RallyAPI.Orders.Domain.Tests;

/// <summary>
/// Pins the weekly <see cref="Payout"/> aggregate — the batch that becomes ONE row in the
/// ICICI bulk-transfer file. The control-sum invariant here is what the bank validates the
/// uploaded file total against, so it must hold exactly with no rounding drift.
/// </summary>
public class PayoutTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);

    private static PayoutLedger Ledger(decimal orderAmount, decimal commissionFlatFee) =>
        PayoutLedger.Create(OwnerId, Guid.NewGuid(), Guid.NewGuid(), orderAmount, commissionFlatFee);

    [Fact]
    public void CreateFromLedger_NetPayout_EqualsSumOfLedgerNets_Exactly()
    {
        // Amounts chosen so each row's commissionGst/tds require rounding — the batch total
        // must still equal the exact sum of the already-rounded row nets (no drift).
        var entries = new[]
        {
            Ledger(333.33m, 33.33m),
            Ledger(500m, 30m),
            Ledger(129.99m, 12.50m),
            Ledger(1000m, 55.55m),
        };

        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, entries, null, null);

        payout.NetPayoutAmount.Should().Be(entries.Sum(e => e.NetAmount));
    }

    [Fact]
    public void CreateFromLedger_AggregatesEveryComponentFromLedger()
    {
        var entries = new[]
        {
            Ledger(500m, 30m),
            Ledger(1000m, 55.55m),
        };

        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, entries, null, null);

        payout.OrderCount.Should().Be(2);
        payout.GrossOrderAmount.Should().Be(entries.Sum(e => e.OrderAmount));
        payout.TotalGstCollected.Should().Be(entries.Sum(e => e.GstAmount));
        payout.TotalCommission.Should().Be(entries.Sum(e => e.CommissionAmount));
        payout.TotalCommissionGst.Should().Be(entries.Sum(e => e.CommissionGst));
        payout.TotalTds.Should().Be(entries.Sum(e => e.TdsAmount));
        payout.Status.Should().Be(PayoutStatus.Pending);
    }

    [Fact]
    public void CreateFromLedger_NetEqualsGrossMinusDeductions()
    {
        var entries = new[] { Ledger(500m, 30m), Ledger(1000m, 55.55m) };

        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, entries, null, null);

        var expectedNet = payout.GrossOrderAmount
                          - payout.TotalCommission
                          - payout.TotalCommissionGst
                          - payout.TotalTds;
        payout.NetPayoutAmount.Should().Be(expectedNet);
    }

    [Fact]
    public void CreateFromLedger_WithNoEntries_Throws()
    {
        var act = () => Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, Array.Empty<PayoutLedger>(), null, null);
        act.Should().Throw<ArgumentException>();
    }

    // --- Lifecycle the ICICI export/reconcile flow depends on ---

    [Fact]
    public void MarkProcessing_FromPending_StampsExportBatch()
    {
        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, new[] { Ledger(500m, 30m) }, null, null);
        var batchId = Guid.NewGuid();

        payout.MarkProcessing(batchId);

        payout.Status.Should().Be(PayoutStatus.Processing);
        payout.ExportBatchId.Should().Be(batchId);
        payout.ExportedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessing_WithEmptyBatchId_Throws()
    {
        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, new[] { Ledger(500m, 30m) }, null, null);

        var act = () => payout.MarkProcessing(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkProcessing_FromNonPending_Throws()
    {
        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, new[] { Ledger(500m, 30m) }, null, null);
        payout.MarkProcessing(Guid.NewGuid());

        // Already Processing — a second export can never re-pick this payout up
        // (the anti-double-pay invariant).
        var act = () => payout.MarkProcessing(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_RequiresProcessing_AndRecordsUtr()
    {
        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, new[] { Ledger(500m, 30m) }, null, null);

        // Cannot pay straight from Pending — export must move it to Processing first.
        var premature = () => payout.MarkPaid("ICICIUTR123");
        premature.Should().Throw<InvalidOperationException>();

        payout.MarkProcessing(Guid.NewGuid());
        payout.MarkPaid("ICICIUTR123");

        payout.Status.Should().Be(PayoutStatus.Paid);
        payout.TransactionReference.Should().Be("ICICIUTR123");
        payout.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_FromProcessing_RecordsReason()
    {
        var payout = Payout.CreateFromLedger(OwnerId, PeriodStart, PeriodEnd, new[] { Ledger(500m, 30m) }, null, null);
        payout.MarkProcessing(Guid.NewGuid());

        payout.MarkFailed("Account frozen at beneficiary bank");

        payout.Status.Should().Be(PayoutStatus.Failed);
        payout.Notes.Should().Be("Account frozen at beneficiary bank");
    }
}
