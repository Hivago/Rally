using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public sealed class RiderPayoutLedgerConfiguration : IEntityTypeConfiguration<RiderPayoutLedger>
{
    public void Configure(EntityTypeBuilder<RiderPayoutLedger> builder)
    {
        builder.ToTable("rider_payout_ledger", "users");

        builder.HasKey(r => r.Id);

        // Optimistic concurrency via Postgres' system xmin column — see
        // RallyAPI.Orders.Infrastructure.Configurations.PayoutConfiguration for the full
        // rationale (same race: two admins exporting the same cycle at once). No DDL.
        builder.UseXminAsConcurrencyToken();

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.RiderId).HasColumnName("rider_id").IsRequired();

        builder.HasOne<Rider>()
            .WithMany()
            .HasForeignKey(r => r.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.CycleStartUtc)
            .HasColumnName("cycle_start").IsRequired();
        builder.Property(r => r.CycleEndUtc)
            .HasColumnName("cycle_end").IsRequired();

        builder.Property(r => r.DeliveryCount)
            .HasColumnName("delivery_count").HasDefaultValue(0).IsRequired();

        builder.Property(r => r.BaseFare)
            .HasColumnName("base_fare").HasPrecision(10, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(r => r.SurgeFare)
            .HasColumnName("surge_fare").HasPrecision(10, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(r => r.Tips)
            .HasColumnName("tips").HasPrecision(10, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(r => r.NetPayable)
            .HasColumnName("net_payable").HasPrecision(10, 2).IsRequired();

        // Stored as a varchar so the DB stays human-readable in pgAdmin and so
        // future enum additions don't shift integer values silently.
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(RiderPayoutStatus.Pending)
            .IsRequired();

        builder.Property(r => r.StatusNote)
            .HasColumnName("status_note").HasMaxLength(500);

        builder.Property(r => r.PaidAtUtc).HasColumnName("paid_at");
        builder.Property(r => r.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(r => r.TransactionReference).HasColumnName("transaction_reference").HasMaxLength(100);

        builder.Property(r => r.ExportBatchId).HasColumnName("export_batch_id");
        builder.Property(r => r.ExportedAtUtc).HasColumnName("exported_at_utc");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(r => r.RiderId).HasDatabaseName("idx_rider_payout_ledger_rider_id");
        builder.HasIndex(r => new { r.CycleStartUtc, r.CycleEndUtc, r.Status })
            .HasDatabaseName("idx_rider_payout_ledger_cycle");
        builder.HasIndex(r => new { r.RiderId, r.CycleStartUtc, r.CycleEndUtc })
            .IsUnique()
            .HasDatabaseName("idx_rider_payout_ledger_rider_cycle");
        builder.HasIndex(r => r.ExportBatchId)
            .HasDatabaseName("idx_rider_payout_ledger_export_batch_id");

        // Same Ignore pattern as Payout / Cart — table doesn't carry these inherited columns.
        // Concurrency is handled via xmin above rather than the app-managed Version counter
        // (which nothing here increments).
        builder.Ignore(r => r.DeletedAt);
        builder.Ignore(r => r.Version);
        builder.Ignore(r => r.DomainEvents);
    }
}
