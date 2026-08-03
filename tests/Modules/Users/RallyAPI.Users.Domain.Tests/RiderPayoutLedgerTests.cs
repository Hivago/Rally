using FluentAssertions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using Xunit;

namespace RallyAPI.Users.Domain.Tests;

/// <summary>
/// Pins the rider payout lifecycle — mirrors PayoutTests on the restaurant side. Riders are
/// settled via the same ICICI weekly export + bank-statement reconciliation flow, so this
/// entity needs the same Pending -> Processing -> Paid/Failed transitions with the same
/// anti-double-export guarantee (export only ever picks Pending).
/// </summary>
public class RiderPayoutLedgerTests
{
    private static readonly Guid RiderId = Guid.NewGuid();
    private static readonly DateTime CycleStart = new(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CycleEnd = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    private static RiderPayoutLedger Ledger(decimal baseFare = 500m, decimal surgeFare = 50m, decimal tips = 20m) =>
        RiderPayoutLedger.Create(RiderId, CycleStart, CycleEnd, deliveryCount: 10, baseFare, surgeFare, tips);

    [Fact]
    public void Create_ComputesNetPayableAsSumOfComponents()
    {
        var ledger = Ledger(500m, 50m, 20m);

        ledger.NetPayable.Should().Be(570m);
        ledger.Status.Should().Be(RiderPayoutStatus.Pending);
    }

    [Fact]
    public void MarkProcessing_FromPending_StampsExportBatch()
    {
        var ledger = Ledger();
        var batchId = Guid.NewGuid();

        ledger.MarkProcessing(batchId);

        ledger.Status.Should().Be(RiderPayoutStatus.Processing);
        ledger.ExportBatchId.Should().Be(batchId);
        ledger.ExportedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessing_WithEmptyBatchId_Throws()
    {
        var ledger = Ledger();

        var act = () => ledger.MarkProcessing(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkProcessing_FromNonPending_Throws()
    {
        var ledger = Ledger();
        ledger.MarkProcessing(Guid.NewGuid());

        // Already Processing — a second export can never re-pick this payout up
        // (the anti-double-pay invariant, same as the restaurant-side Payout).
        var act = () => ledger.MarkProcessing(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_RequiresProcessing_AndRecordsUtr()
    {
        var ledger = Ledger();

        // Cannot pay straight from Pending — export must move it to Processing first.
        var premature = () => ledger.MarkPaid("ICICIUTR123");
        premature.Should().Throw<InvalidOperationException>();

        ledger.MarkProcessing(Guid.NewGuid());
        ledger.MarkPaid("ICICIUTR123");

        ledger.Status.Should().Be(RiderPayoutStatus.Paid);
        ledger.TransactionReference.Should().Be("ICICIUTR123");
        ledger.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkPaid_WithBlankReference_Throws()
    {
        var ledger = Ledger();
        ledger.MarkProcessing(Guid.NewGuid());

        var act = () => ledger.MarkPaid("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFailed_FromProcessing_RecordsReason()
    {
        var ledger = Ledger();
        ledger.MarkProcessing(Guid.NewGuid());

        ledger.MarkFailed("Account frozen at beneficiary bank");

        ledger.Status.Should().Be(RiderPayoutStatus.Failed);
        ledger.FailureReason.Should().Be("Account frozen at beneficiary bank");
    }

    [Fact]
    public void MarkRetry_FromFailed_ReturnsToPending()
    {
        var ledger = Ledger();
        ledger.MarkProcessing(Guid.NewGuid());
        ledger.MarkFailed("Bad IFSC");

        ledger.MarkRetry();

        ledger.Status.Should().Be(RiderPayoutStatus.Pending);
        ledger.FailureReason.Should().BeNull();
    }
}
