using FluentAssertions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using Xunit;

namespace RallyAPI.Users.Domain.Tests;

/// <summary>
/// Mirrors RallyAPI.Orders.Domain.Tests.RestaurantPayoutExportBatchTests — pins the same
/// audit-trail invariants on the rider-side export batch record.
/// </summary>
public class RiderPayoutExportBatchTests
{
    private static readonly DateOnly PeriodStart = new(2026, 7, 13);
    private static readonly DateOnly PeriodEnd = new(2026, 7, 19);
    private static readonly Guid AdminId = Guid.NewGuid();

    private static RiderPayoutExportBatch Batch() =>
        RiderPayoutExportBatch.Create(PeriodStart, PeriodEnd, rowCount: 5, controlSumTotal: 2500m, AdminId, "abc123hash");

    [Fact]
    public void Create_WithValidInputs_SetsGeneratedStateCorrectly()
    {
        var batch = Batch();

        batch.Status.Should().Be(PayoutExportBatchStatus.Generated);
        batch.RowCount.Should().Be(5);
        batch.ControlSumTotal.Should().Be(2500m);
        batch.GeneratedByAdminId.Should().Be(AdminId);
        batch.GeneratedFileHash.Should().Be("abc123hash");
        batch.ReconciledAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithPeriodEndBeforeStart_Throws()
    {
        var act = () => RiderPayoutExportBatch.Create(PeriodEnd, PeriodStart, 1, 100m, AdminId, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroRowCount_Throws()
    {
        var act = () => RiderPayoutExportBatch.Create(PeriodStart, PeriodEnd, 0, 100m, AdminId, "hash");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonPositiveControlSum_Throws()
    {
        var act = () => RiderPayoutExportBatch.Create(PeriodStart, PeriodEnd, 1, 0m, AdminId, "hash");
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
    }

    [Fact]
    public void MarkReconciled_Twice_Throws()
    {
        var batch = Batch();
        batch.MarkReconciled(Guid.NewGuid(), "hash-1");

        var act = () => batch.MarkReconciled(Guid.NewGuid(), "hash-2");

        act.Should().Throw<InvalidOperationException>();
    }
}
