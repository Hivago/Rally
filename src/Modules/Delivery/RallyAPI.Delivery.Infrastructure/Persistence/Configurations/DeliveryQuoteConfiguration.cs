using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Infrastructure.Persistence.Configurations;

public sealed class DeliveryQuoteConfiguration : IEntityTypeConfiguration<DeliveryQuote>
{
    public void Configure(EntityTypeBuilder<DeliveryQuote> builder)
    {
        builder.ToTable("quotes");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Location - Pickup
        builder.Property(q => q.PickupLatitude)
            .HasColumnName("pickup_latitude")
            .IsRequired();

        builder.Property(q => q.PickupLongitude)
            .HasColumnName("pickup_longitude")
            .IsRequired();

        builder.Property(q => q.PickupPincode)
            .HasColumnName("pickup_pincode")
            .HasMaxLength(10)
            .IsRequired();

        // Location - Drop
        builder.Property(q => q.DropLatitude)
            .HasColumnName("drop_latitude")
            .IsRequired();

        builder.Property(q => q.DropLongitude)
            .HasColumnName("drop_longitude")
            .IsRequired();

        builder.Property(q => q.DropPincode)
            .HasColumnName("drop_pincode")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(q => q.City)
            .HasColumnName("city")
            .HasMaxLength(100)
            .IsRequired();

        // Order Info
        builder.Property(q => q.OrderAmount)
            .HasColumnName("order_amount")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(q => q.RestaurantId)
            .HasColumnName("restaurant_id");

        // Distance & Pricing
        builder.Property(q => q.DistanceKm)
            .HasColumnName("distance_km")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(q => q.BaseFee)
            .HasColumnName("base_fee")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(q => q.FinalFee)
            .HasColumnName("final_fee")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(q => q.SurgeMultiplier)
            .HasColumnName("surge_multiplier")
            .HasPrecision(4, 2)
            .HasDefaultValue(1.0m)
            .IsRequired();

        builder.Property(q => q.SurgeReason)
            .HasColumnName("surge_reason")
            .HasMaxLength(200);

        builder.Property(q => q.EstimatedMinutes)
            .HasColumnName("estimated_minutes")
            .IsRequired();

        // Fleet Info
        builder.Property(q => q.FleetType)
            .HasColumnName("fleet_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(q => q.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(q => q.ProviderQuoteId)
            .HasColumnName("provider_quote_id")
            .HasMaxLength(100);

        // Validity
        builder.Property(q => q.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(q => q.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(q => q.IsUsed)
            .HasColumnName("is_used")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.UsedAt)
            .HasColumnName("used_at");

        builder.Property(q => q.UsedForOrderId)
            .HasColumnName("used_for_order_id");

        // Breakdown JSON
        builder.Property(q => q.BreakdownJson)
            .HasColumnName("breakdown")
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(q => q.ExpiresAt)
            .HasDatabaseName("ix_quotes_expires_at");

        builder.HasIndex(q => q.IsUsed)
            .HasDatabaseName("ix_quotes_is_used");

        builder.HasIndex(q => q.UsedForOrderId)
            .HasDatabaseName("ix_quotes_order_id");
    }
}