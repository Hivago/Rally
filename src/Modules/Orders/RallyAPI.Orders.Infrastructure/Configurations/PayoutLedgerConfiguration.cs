using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Infrastructure.Configurations;

public sealed class PayoutLedgerConfiguration : IEntityTypeConfiguration<PayoutLedger>
{
    public void Configure(EntityTypeBuilder<PayoutLedger> builder)
    {
        builder.ToTable("payout_ledger", "orders");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(l => l.OutletId)
            .HasColumnName("outlet_id")
            .IsRequired();

        builder.Property(l => l.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(l => l.OrderAmount)
            .HasColumnName("order_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.GstAmount)
            .HasColumnName("gst_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.CommissionPercentage)
            .HasColumnName("commission_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(l => l.CommissionFlatFee)
            .HasColumnName("commission_flat_fee")
            .HasPrecision(10, 2)
            .IsRequired(false);

        builder.Property(l => l.CommissionAmount)
            .HasColumnName("commission_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.CommissionGst)
            .HasColumnName("commission_gst")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.TdsAmount)
            .HasColumnName("tds_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.NetAmount)
            .HasColumnName("net_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(l => l.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("INR")
            .IsRequired();

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.PayoutId)
            .HasColumnName("payout_id");

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(l => l.OwnerId)
            .HasDatabaseName("ix_payout_ledger_owner_id");

        builder.HasIndex(l => l.OutletId)
            .HasDatabaseName("ix_payout_ledger_outlet_id");

        builder.HasIndex(l => l.OrderId)
            .IsUnique()
            .HasDatabaseName("ix_payout_ledger_order_id");

        builder.HasIndex(l => l.PayoutId)
            .HasDatabaseName("ix_payout_ledger_payout_id");

        builder.HasIndex(l => new { l.OwnerId, l.Status })
            .HasDatabaseName("ix_payout_ledger_owner_status");

        // Ledger rows are not soft-deleted (PayoutLedger extends BaseEntity which exposes
        // DeletedAt; the column was never added to orders.payout_ledger).
        builder.Ignore(l => l.DeletedAt);
    }
}
