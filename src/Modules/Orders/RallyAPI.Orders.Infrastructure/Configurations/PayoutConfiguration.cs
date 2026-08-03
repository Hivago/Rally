using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Infrastructure.Configurations;

public sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts", "orders");

        builder.HasKey(p => p.Id);

        // Optimistic concurrency via Postgres' system xmin column. Without this, two admins
        // exporting the same period at once would both load this row as Pending and both
        // write Processing — last write wins, silently. With xmin, EF emits
        // WHERE xmin = @original on the UPDATE, so the losing SaveChangesAsync throws
        // DbUpdateConcurrencyException instead of letting the payout end up counted in two
        // export files. No DDL: xmin is a built-in system column. Same fix as
        // DeliveryRequestConfiguration (commit b78e764) for the analogous dispatch race.
        builder.UseXminAsConcurrencyToken();

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(p => p.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(p => p.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(p => p.OrderCount)
            .HasColumnName("order_count")
            .IsRequired();

        builder.Property(p => p.GrossOrderAmount)
            .HasColumnName("gross_order_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.TotalGstCollected)
            .HasColumnName("total_gst_collected")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.TotalCommission)
            .HasColumnName("total_commission")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.TotalCommissionGst)
            .HasColumnName("total_commission_gst")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.TotalTds)
            .HasColumnName("total_tds")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.NetPayoutAmount)
            .HasColumnName("net_payout_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.BankAccountNumber)
            .HasColumnName("bank_account_number")
            .HasMaxLength(20);

        builder.Property(p => p.BankIfscCode)
            .HasColumnName("bank_ifsc_code")
            .HasMaxLength(11);

        builder.Property(p => p.TransactionReference)
            .HasColumnName("transaction_reference")
            .HasMaxLength(100);

        builder.Property(p => p.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(p => p.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(p => p.ExportBatchId)
            .HasColumnName("export_batch_id");

        builder.Property(p => p.ExportedAtUtc)
            .HasColumnName("exported_at_utc");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("ix_payouts_owner_id");

        // Unique: prevents two Payout rows for the same owner+period, which would export as
        // two separate ICICI transfer rows to the same beneficiary (a real double-payment
        // risk). Verified clean against prod before adding (2026-07-22, zero duplicates).
        builder.HasIndex(p => new { p.OwnerId, p.PeriodStart, p.PeriodEnd })
            .IsUnique()
            .HasDatabaseName("ix_payouts_owner_period");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_payouts_status");

        builder.HasIndex(p => p.ExportBatchId)
            .HasDatabaseName("ix_payouts_export_batch_id");

        // Relationship: Payout has many ledger entries
        builder.HasMany<PayoutLedger>()
            .WithOne()
            .HasForeignKey(l => l.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore domain events
        builder.Ignore(p => p.DomainEvents);

        // Payouts are not soft-deleted, and concurrency is handled via xmin above rather
        // than the app-managed Version counter (which nothing here increments). The columns
        // inherited from BaseEntity were never added to orders.payouts; explicit Ignore lets
        // EF skip them in SELECT/INSERT (matches Cart/CartItem pattern).
        builder.Ignore(p => p.DeletedAt);
        builder.Ignore(p => p.Version);
    }
}
