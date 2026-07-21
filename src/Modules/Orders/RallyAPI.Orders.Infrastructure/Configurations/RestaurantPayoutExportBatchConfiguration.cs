using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Infrastructure.Configurations;

public sealed class RestaurantPayoutExportBatchConfiguration : IEntityTypeConfiguration<RestaurantPayoutExportBatch>
{
    public void Configure(EntityTypeBuilder<RestaurantPayoutExportBatch> builder)
    {
        builder.ToTable("restaurant_payout_export_batches", "orders");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(b => b.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(b => b.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(b => b.RowCount)
            .HasColumnName("row_count")
            .IsRequired();

        builder.Property(b => b.ControlSumTotal)
            .HasColumnName("control_sum_total")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(b => b.GeneratedByAdminId)
            .HasColumnName("generated_by_admin_id")
            .IsRequired();

        builder.Property(b => b.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .IsRequired();

        builder.Property(b => b.GeneratedFileHash)
            .HasColumnName("generated_file_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(b => b.ReconciledByAdminId)
            .HasColumnName("reconciled_by_admin_id");

        builder.Property(b => b.ReconciledAtUtc)
            .HasColumnName("reconciled_at_utc");

        builder.Property(b => b.ReconciliationFileHash)
            .HasColumnName("reconciliation_file_hash")
            .HasMaxLength(64);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(b => new { b.PeriodStart, b.PeriodEnd })
            .HasDatabaseName("ix_restaurant_payout_export_batches_period");

        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_restaurant_payout_export_batches_status");

        builder.Ignore(b => b.DomainEvents);
        builder.Ignore(b => b.DeletedAt);
        builder.Ignore(b => b.Version);
    }
}
