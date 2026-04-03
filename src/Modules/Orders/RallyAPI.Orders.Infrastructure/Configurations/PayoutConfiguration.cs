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

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(p => p.OwnerId)
            .HasDatabaseName("ix_payouts_owner_id");

        builder.HasIndex(p => new { p.OwnerId, p.PeriodStart, p.PeriodEnd })
            .HasDatabaseName("ix_payouts_owner_period");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_payouts_status");

        // Relationship: Payout has many ledger entries
        builder.HasMany<PayoutLedger>()
            .WithOne()
            .HasForeignKey(l => l.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore domain events
        builder.Ignore(p => p.DomainEvents);
    }
}
