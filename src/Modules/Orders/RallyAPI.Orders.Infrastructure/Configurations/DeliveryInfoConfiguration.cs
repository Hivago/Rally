using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.ValueObjects;

namespace RallyAPI.Orders.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for DeliveryInfo entity.
/// </summary>
public sealed class DeliveryInfoConfiguration : IEntityTypeConfiguration<DeliveryInfo>
{
    public void Configure(EntityTypeBuilder<DeliveryInfo> builder)
    {
        builder.ToTable("delivery_info");

        // Primary Key
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Foreign Key (shadow property)
        builder.Property<Guid>("OrderId")
            .HasColumnName("order_id")
            .IsRequired();

        builder.HasIndex("OrderId")
            .IsUnique()
            .HasDatabaseName("ix_delivery_info_order_id");

        // Pickup Location (Value Object)
        builder.OwnsOne(d => d.PickupLocation, loc =>
        {
            loc.Property(l => l.Latitude)
                .HasColumnName("pickup_latitude")
                .IsRequired();
            loc.Property(l => l.Longitude)
                .HasColumnName("pickup_longitude")
                .IsRequired();
        });

        builder.Property(d => d.PickupPincode)
            .HasColumnName("pickup_pincode")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.PickupAddress)
            .HasColumnName("pickup_address")
            .HasMaxLength(500);

        builder.Property(d => d.PickupContactPhone)
            .HasColumnName("pickup_contact_phone")
            .HasMaxLength(20);

        // Delivery Address (Value Object)
        builder.OwnsOne(d => d.DeliveryAddress, addr =>
        {
            addr.Property(a => a.Street)
                .HasColumnName("delivery_street")
                .HasMaxLength(255)
                .IsRequired();
            addr.Property(a => a.City)
                .HasColumnName("delivery_city")
                .HasMaxLength(100)
                .IsRequired();
            addr.Property(a => a.Pincode)
                .HasColumnName("delivery_pincode")
                .HasMaxLength(10)
                .IsRequired();
            addr.Property(a => a.Latitude)
                .HasColumnName("delivery_latitude")
                .IsRequired();
            addr.Property(a => a.Longitude)
                .HasColumnName("delivery_longitude")
                .IsRequired();
            addr.Property(a => a.Landmark)
                .HasColumnName("delivery_landmark")
                .HasMaxLength(200);
            addr.Property(a => a.BuildingName)
                .HasColumnName("delivery_building_name")
                .HasMaxLength(200);
            addr.Property(a => a.Floor)
                .HasColumnName("delivery_floor")
                .HasMaxLength(20);
            addr.Property(a => a.ContactPhone)
                .HasColumnName("delivery_contact_phone")
                .HasMaxLength(20);
            addr.Property(a => a.Instructions)
                .HasColumnName("delivery_instructions")
                .HasMaxLength(500);
        });

        // Quote Info
        builder.Property(d => d.QuoteId)
            .HasColumnName("quote_id")
            .HasMaxLength(100);

        builder.Property(d => d.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100);

        builder.OwnsOne(d => d.QuotedDeliveryFee, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("quoted_delivery_fee")
                .HasPrecision(10, 2);
            money.Property(m => m.Currency)
                .HasColumnName("quoted_delivery_fee_currency")
                .HasMaxLength(3)
                .HasDefaultValue("INR");
        }); 

        builder.Property(d => d.EstimatedMinutes)
            .HasColumnName("estimated_minutes");

        builder.Property(d => d.QuotedAt)
            .HasColumnName("quoted_at");

        // Rider Info
        builder.Property(d => d.RiderId)
            .HasColumnName("rider_id");

        builder.HasIndex(d => d.RiderId)
            .HasDatabaseName("ix_delivery_info_rider_id");

        builder.Property(d => d.RiderName)
            .HasColumnName("rider_name")
            .HasMaxLength(200);

        builder.Property(d => d.RiderPhone)
            .HasColumnName("rider_phone")
            .HasMaxLength(20);

        builder.Property(d => d.TrackingUrl)
            .HasColumnName("tracking_url")
            .HasMaxLength(500);

        // Timestamps
        builder.Property(d => d.AssignedAt)
            .HasColumnName("assigned_at");

        builder.Property(d => d.PickedUpAt)
            .HasColumnName("picked_up_at");

        builder.Property(d => d.DeliveredAt)
            .HasColumnName("delivered_at");

        // Distance
        builder.Property(d => d.DistanceKm)
            .HasColumnName("distance_km")
            .HasPrecision(8, 2);
    }
}