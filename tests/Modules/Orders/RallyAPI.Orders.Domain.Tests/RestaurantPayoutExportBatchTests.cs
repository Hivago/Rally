using FluentAssertions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using Xunit;

namespace RallyAPI.Orders.Domain.Tests;

/// <summary>
/// Pins the audit-trail invariants for the ICICI export batch record: every field the
/// security model in specs/icici-manual-payout-export.md depends on (who generated it, its
/// control-sum, its file hash, who reconciled it and with what) must actually be enforced,
/// not just present on the type.
/// </summary>
public class RestaurantPayoutExportBatchTests
{
    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    private static RestaurantPayoutExportBatch Batch() =>
        RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, rowCount: 3, controlSumTotal: 1708.80m, AdminId, "abc123hash");

    [Fact]
    public void Create_WithValidInputs_SetsGeneratedStateCorrectly()
    {
        var batch = Batch();

        batch.Status.Should().Be(PayoutExportBatchStatus.Generated);
        batch.RowCount.Should().Be(3);
        batch.ControlSumTotal.Should().Be(1708.80m);
        batch.GeneratedByAdminId.Should().Be(AdminId);
        batch.GeneratedFileHash.Should().Be("abc123hash");
        batch.ReconciledAtUtc.Should().BeNull();
        batch.ReconciledByAdminId.Should().BeNull();
    }

    [Fact]
    public void Create_WithPeriodEndBeforeStart_Throws()
    {
        var act = () => RestaurantPayoutExportBatch.Create(PeriodEnd, PeriodStart, 1, 100m, AdminId, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroRowCount_Throws()
    {
        var act = () => RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 0, 100m, AdminId, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonPositiveControlSum_Throws()
    {
        var act = () => RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 0m, AdminId, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyAdminId_Throws()
    {
        var act = () => RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 100m, Guid.Empty, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithBlankFileHash_Throws()
    {
        var act = () => RestaurantPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 100m, AdminId, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkReconciled_FromGenerated_RecordsReconcilerAndHash()
    {
        var batch = Batch();
        var reconciler = Guid.NewGuid();

        batch.MarkReconciled(reconciler, "statement-hash-xyz");

        batch.Status.Should().Be(PayoutExportBatchStatus.Reconciled);
        batch.ReconciledByAdminId.Should().Be(reconciler);
        batch.ReconciliationFileHash.Should().Be("statement-hash-xyz");
        batch.ReconciledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkReconciled_Twice_Throws()
    {
        var batch = Batch();
        batch.MarkReconciled(Guid.NewGuid(), "hash-1");

        var act = () => batch.MarkReconciled(Guid.NewGuid(), "hash-2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReconciled_WithEmptyAdminId_Throws()
    {
        var batch = Batch();
        var act = () => batch.MarkReconciled(Guid.Empty, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkReconciled_WithBlankFileHash_Throws()
    {
        var batch = Batch();
        var act = () => batch.MarkReconciled(Guid.NewGuid(), "  ");
        act.Should().Throw<ArgumentException>();
    }
}
