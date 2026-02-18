using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for OrderItem entity.
/// </summary>
public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        // Primary Key
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Foreign Key (shadow property)
        builder.Property<Guid>("OrderId")
            .HasColumnName("order_id")
            .IsRequired();

        builder.HasIndex("OrderId")
            .HasDatabaseName("ix_order_items_order_id");

        // Menu Item Reference
        builder.Property(i => i.MenuItemId)
            .HasColumnName("menu_item_id")
            .IsRequired();

        // Denormalized Item Info
        builder.Property(i => i.ItemName)
            .HasColumnName("item_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.ItemDescription)
            .HasColumnName("item_description")
            .HasMaxLength(500);

        builder.Property(i => i.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        // Pricing (Value Objects)
        builder.OwnsOne(i => i.UnitPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("unit_price")
                .HasPrecision(10, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(i => i.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.OwnsOne(i => i.TotalPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("total_price")
                .HasPrecision(10, 2)
                .IsRequired();
            money.Property(m => m.Currency)
            .HasColumnName("total_price_currency")
            .HasMaxLength(3)
            .HasDefaultValue("INR");
        });

        // Instructions
        builder.Property(i => i.SpecialInstructions)
            .HasColumnName("special_instructions")
            .HasMaxLength(500);

        // Metadata
        builder.Property(i => i.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");
    }
}