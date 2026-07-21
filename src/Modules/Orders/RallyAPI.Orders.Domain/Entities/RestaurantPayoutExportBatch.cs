using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Orders.Domain.Entities;

/// <summary>
/// One weekly ICICI bulk-transfer export for restaurant payouts. Every <see cref="Payout"/>
/// included in the export stamps its ExportBatchId to this record's Id, so the file that went
/// out, its control-sum, and its later reconciliation are all traceable from one row — the
/// audit trail the manual-payout security model depends on (see
/// specs/icici-manual-payout-export.md, section 4a).
/// </summary>
public sealed class RestaurantPayoutExportBatch : AggregateRoot
{
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public int RowCount { get; private set; }
    public decimal ControlSumTotal { get; private set; }
    public PayoutExportBatchStatus Status { get; private set; }

    public Guid GeneratedByAdminId { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }

    /// <summary>SHA-256 hash of the generated .xlsx, for later comparison against what was actually uploaded to ICICI.</summary>
    public string GeneratedFileHash { get; private set; } = null!;

    public Guid? ReconciledByAdminId { get; private set; }
    public DateTime? ReconciledAtUtc { get; private set; }

    /// <summary>SHA-256 hash of the uploaded ICICI bank statement used to reconcile this batch.</summary>
    public string? ReconciliationFileHash { get; private set; }

    // EF Core
    private RestaurantPayoutExportBatch() { }

    public static RestaurantPayoutExportBatch Create(
        DateOnly periodStart,
        DateOnly periodEnd,
        int rowCount,
        decimal controlSumTotal,
        Guid generatedByAdminId,
        string generatedFileHash)
    {
        if (periodStart >= periodEnd)
            throw new ArgumentException("Period start must be before period end.");

        if (rowCount <= 0)
            throw new ArgumentException("Row count must be positive.", nameof(rowCount));

        if (controlSumTotal <= 0)
            throw new ArgumentException("Control-sum total must be positive.", nameof(controlSumTotal));

        if (generatedByAdminId == Guid.Empty)
            throw new ArgumentException("Generating admin ID is required.", nameof(generatedByAdminId));

        if (string.IsNullOrWhiteSpace(generatedFileHash))
            throw new ArgumentException("Generated file hash is required.", nameof(generatedFileHash));

        return new RestaurantPayoutExportBatch
        {
            Id = Guid.NewGuid(),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RowCount = rowCount,
            ControlSumTotal = controlSumTotal,
            Status = PayoutExportBatchStatus.Generated,
            GeneratedByAdminId = generatedByAdminId,
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedFileHash = generatedFileHash.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Called once every payout row in this batch has been resolved (Paid or Failed) by
    /// reconciling the ICICI bank statement.
    /// </summary>
    public void MarkReconciled(Guid reconciledByAdminId, string reconciliationFileHash)
    {
        if (Status != PayoutExportBatchStatus.Generated)
            throw new InvalidOperationException($"Cannot reconcile a batch in {Status} status.");

        if (reconciledByAdminId == Guid.Empty)
            throw new ArgumentException("Reconciling admin ID is required.", nameof(reconciledByAdminId));

        if (string.IsNullOrWhiteSpace(reconciliationFileHash))
            throw new ArgumentException("Reconciliation file hash is required.", nameof(reconciliationFileHash));

        Status = PayoutExportBatchStatus.Reconciled;
        ReconciledByAdminId = reconciledByAdminId;
        ReconciledAtUtc = DateTime.UtcNow;
        ReconciliationFileHash = reconciliationFileHash.Trim();
        MarkAsUpdated();
    }
}
