using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for Order entity.
/// Maps value objects and configures relationships.
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        // Primary Key
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Order Number (Value Object)
        builder.Property(o => o.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => OrderNumber.From(v));

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("ix_orders_order_number");

        // Customer Info
        builder.Property(o => o.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("ix_orders_customer_id");

        builder.Property(o => o.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.CustomerPhone)
            .HasColumnName("customer_phone")
            .HasMaxLength(20);

        builder.Property(o => o.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(255);

        // Restaurant Info
        builder.Property(o => o.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.HasIndex(o => o.RestaurantId)
            .HasDatabaseName("ix_orders_restaurant_id");

        builder.Property(o => o.RestaurantName)
            .HasColumnName("restaurant_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.RestaurantPhone)
            .HasColumnName("restaurant_phone")
            .HasMaxLength(20);

        // Status
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_orders_status");

        builder.Property(o => o.PaymentStatus)
            .HasColumnName("payment_status")
            .HasConversion<int>()
            .IsRequired();

        // Pricing (Owned Entity / Complex Property)
        builder.OwnsOne(o => o.Pricing, pricing =>
        {
            pricing.OwnsOne(p => p.SubTotal, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("sub_total")
                    .HasPrecision(10, 2)
                    .IsRequired();
                money.Property(m => m.Currency)
                    .HasColumnName("currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            //pricing.OwnsOne(p => p.DeliveryFee, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("delivery_fee")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.Tax, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("tax")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.Discount, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("discount")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.PackagingFee, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("packaging_fee")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.ServiceFee, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("service_fee")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.Tip, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("tip")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            //pricing.OwnsOne(p => p.Total, money =>
            //{
            //    money.Property(m => m.Amount)
            //        .HasColumnName("total")
            //        .HasPrecision(10, 2)
            //        .IsRequired();
            //    money.Ignore(m => m.Currency);
            //});

            pricing.OwnsOne(p => p.DeliveryFee, money =>
            {
                money.Property(m => m.Amount).HasColumnName("delivery_fee").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("delivery_fee_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.Tax, money =>
            {
                money.Property(m => m.Amount).HasColumnName("tax").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("tax_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.Discount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("discount").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("discount_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.PackagingFee, money =>
            {
                money.Property(m => m.Amount).HasColumnName("packaging_fee").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("packaging_fee_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.ServiceFee, money =>
            {
                money.Property(m => m.Amount).HasColumnName("service_fee").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("service_fee_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.Tip, money =>
            {
                money.Property(m => m.Amount).HasColumnName("tip").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("tip_currency").HasMaxLength(3).HasDefaultValue("INR");
            });
            pricing.OwnsOne(p => p.Total, money =>
            {
                money.Property(m => m.Amount).HasColumnName("total").HasPrecision(10, 2).IsRequired();
                money.Property(m => m.Currency).HasColumnName("total_currency").HasMaxLength(3).HasDefaultValue("INR");
            });

            pricing.Property(p => p.DiscountCode)
                .HasColumnName("discount_code")
                .HasMaxLength(50);

            pricing.Property(p => p.DiscountDescription)
                .HasColumnName("discount_description")
                .HasMaxLength(255);
        });

        // Delivery Info (Owned Entity)
        builder.HasOne(o => o.DeliveryInfo)
            .WithOne()
            .HasForeignKey<DeliveryInfo>("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        // Items (One-to-Many)
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        // Timestamps
        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(o => o.CreatedAt)
            .HasDatabaseName("ix_orders_created_at");

        builder.Property(o => o.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.Property(o => o.PreparingAt)
            .HasColumnName("preparing_at");

        builder.Property(o => o.ReadyAt)
            .HasColumnName("ready_at");

        builder.Property(o => o.PickedUpAt)
            .HasColumnName("picked_up_at");

        builder.Property(o => o.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(o => o.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Cancellation
        builder.Property(o => o.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasConversion<int?>();

        builder.Property(o => o.CancellationNotes)
            .HasColumnName("cancellation_notes")
            .HasMaxLength(1000);

        builder.Property(o => o.CancelledBy)
            .HasColumnName("cancelled_by");

        // Notes
        builder.Property(o => o.SpecialInstructions)
            .HasColumnName("special_instructions")
            .HasMaxLength(1000);

        builder.Property(o => o.InternalNotes)
            .HasColumnName("internal_notes")
            .HasMaxLength(2000);

        // Metadata
        builder.Property(o => o.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Ignore domain events
        builder.Ignore(o => o.DomainEvents);
    }
}