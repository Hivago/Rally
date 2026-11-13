// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/Configurations/PaymentConfiguration.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Infrastructure.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "orders");

        builder.HasKey(p => p.Id);

        // Optimistic concurrency via Postgres' xmin system column (same pattern as
        // PayoutConfiguration). Two simultaneous PayU webhooks that both read this row as
        // Pending would otherwise both write Paid; with xmin the loser's UPDATE matches 0 rows
        // and SaveChangesAsync throws DbUpdateConcurrencyException. No DDL: xmin is built in.
        builder.UseXminAsConcurrencyToken();


        builder.Property(p => p.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(p => p.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(p => p.TxnId)
            .HasColumnName("txn_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.PayuId)
            .HasColumnName("payu_id")
            .HasMaxLength(50);

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("INR")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.PayuStatus)
            .HasColumnName("payu_status")
            .HasMaxLength(20);

        builder.Property(p => p.PaymentMode)
            .HasColumnName("payment_mode")
            .HasMaxLength(20);

        builder.Property(p => p.BankRefNum)
            .HasColumnName("bank_ref_num")
            .HasMaxLength(100);

        builder.Property(p => p.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(500);

        builder.Property(p => p.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(100);

        builder.Property(p => p.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(255);

        builder.Property(p => p.CustomerPhone)
            .HasColumnName("customer_phone")
            .HasMaxLength(15);

        builder.Property(p => p.WebhookFailed)
            .HasColumnName("webhook_failed")
            .HasDefaultValue(false);

        builder.Property(p => p.RefundRequestId)
            .HasColumnName("refund_request_id")
            .HasMaxLength(50);

        builder.Property(p => p.RefundAmount)
            .HasColumnName("refund_amount")
            .HasColumnType("numeric(10,2)");

        builder.Property(p => p.RefundStatus)
            .HasColumnName("refund_status")
            .HasMaxLength(20);

        builder.Property(p => p.RefundedAt)
            .HasColumnName("refunded_at");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(p => p.OrderId)
            .HasDatabaseName("ix_payments_order_id")
            .IsUnique();

        builder.HasIndex(p => p.TxnId)
            .HasDatabaseName("ix_payments_txn_id")
            .IsUnique();

        builder.HasIndex(p => p.CustomerId)
            .HasDatabaseName("ix_payments_customer_id");
    }
}