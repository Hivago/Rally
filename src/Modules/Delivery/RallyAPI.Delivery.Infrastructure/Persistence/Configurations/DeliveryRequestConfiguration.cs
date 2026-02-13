using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Infrastructure.Persistence.Configurations;

public sealed class DeliveryRequestConfiguration : IEntityTypeConfiguration<DeliveryRequest>
{
    public void Configure(EntityTypeBuilder<DeliveryRequest> builder)
    {
        builder.ToTable("delivery_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Order Reference
        builder.Property(r => r.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(r => r.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.QuoteId)
            .HasColumnName("quote_id");

        // Order Context
        builder.Property(r => r.RestaurantId)
            .HasColumnName("restaurant_id");

        builder.Property(r => r.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(r => r.ItemCount)
            .HasColumnName("item_count");

        builder.Property(r => r.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(10, 2);

        builder.Property(r => r.DeliveryInstructions)
            .HasColumnName("delivery_instructions")
            .HasMaxLength(500);

        // Status
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.FleetType)
            .HasColumnName("fleet_type")
            .HasConversion<int>();

        // Pricing
        builder.Property(r => r.QuotedPrice)
            .HasColumnName("quoted_price")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(r => r.ActualPrice)
            .HasColumnName("actual_price")
            .HasPrecision(10, 2);

        builder.Property(r => r.PriceDifference)
            .HasColumnName("price_difference")
            .HasPrecision(10, 2);

        // Own Fleet Assignment
        builder.Property(r => r.RiderId)
            .HasColumnName("rider_id");

        builder.Property(r => r.RiderName)
            .HasColumnName("rider_name")
            .HasMaxLength(200);

        builder.Property(r => r.RiderPhone)
            .HasColumnName("rider_phone")
            .HasMaxLength(20);

        // 3PL Assignment
        builder.Property(r => r.ExternalTaskId)
            .HasColumnName("external_task_id")
            .HasMaxLength(100);

        builder.Property(r => r.ExternalTrackingUrl)
            .HasColumnName("external_tracking_url")
            .HasMaxLength(500);

        builder.Property(r => r.ExternalRiderName)
            .HasColumnName("external_rider_name")
            .HasMaxLength(200);

        builder.Property(r => r.ExternalRiderPhone)
            .HasColumnName("external_rider_phone")
            .HasMaxLength(20);

        builder.Property(r => r.ExternalLspName)
            .HasColumnName("external_lsp_name")
            .HasMaxLength(100);

        // Pickup Location
        builder.Property(r => r.PickupLatitude)
            .HasColumnName("pickup_latitude")
            .IsRequired();

        builder.Property(r => r.PickupLongitude)
            .HasColumnName("pickup_longitude")
            .IsRequired();

        builder.Property(r => r.PickupPincode)
            .HasColumnName("pickup_pincode")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.PickupAddress)
            .HasColumnName("pickup_address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.PickupContactName)
            .HasColumnName("pickup_contact_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.PickupContactPhone)
            .HasColumnName("pickup_contact_phone")
            .HasMaxLength(20)
            .IsRequired();

        // Drop Location
        builder.Property(r => r.DropLatitude)
            .HasColumnName("drop_latitude")
            .IsRequired();

        builder.Property(r => r.DropLongitude)
            .HasColumnName("drop_longitude")
            .IsRequired();

        builder.Property(r => r.DropPincode)
            .HasColumnName("drop_pincode")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.DropAddress)
            .HasColumnName("drop_address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.DropContactName)
            .HasColumnName("drop_contact_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.DropContactPhone)
            .HasColumnName("drop_contact_phone")
            .HasMaxLength(20)
            .IsRequired();

        // Timestamps
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DispatchAt)
            .HasColumnName("dispatch_at");

        builder.Property(r => r.SearchingStartedAt)
            .HasColumnName("searching_started_at");

        builder.Property(r => r.AssignedAt)
            .HasColumnName("assigned_at");

        builder.Property(r => r.ArrivedPickupAt)
            .HasColumnName("arrived_pickup_at");

        builder.Property(r => r.PickedUpAt)
            .HasColumnName("picked_up_at");

        builder.Property(r => r.ArrivedDropAt)
            .HasColumnName("arrived_drop_at");

        builder.Property(r => r.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(r => r.FailedAt)
            .HasColumnName("failed_at");

        builder.Property(r => r.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Failure Info
        builder.Property(r => r.FailureReason)
            .HasColumnName("failure_reason")
            .HasConversion<int>();

        builder.Property(r => r.FailureNotes)
            .HasColumnName("failure_notes")
            .HasMaxLength(500);

        builder.Property(r => r.FailurePhotoUrl)
            .HasColumnName("failure_photo_url")
            .HasMaxLength(500);

        // Retry Tracking
        builder.Property(r => r.OwnFleetAttempts)
            .HasColumnName("own_fleet_attempts")
            .HasDefaultValue(0)
            .IsRequired();

        // Distance
        builder.Property(r => r.DistanceKm)
            .HasColumnName("distance_km")
            .HasPrecision(8, 2);

        builder.Property(r => r.EstimatedMinutes)
            .HasColumnName("estimated_minutes");

        // Navigation
        builder.HasMany(r => r.RiderOffers)
            .WithOne()
            .HasForeignKey(o => o.DeliveryRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events
        builder.Ignore(r => r.DomainEvents);

        // Indexes
        builder.HasIndex(r => r.OrderId)
            .IsUnique()
            .HasDatabaseName("ix_delivery_requests_order_id");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_delivery_requests_status");

        builder.HasIndex(r => r.RiderId)
            .HasDatabaseName("ix_delivery_requests_rider_id");

        builder.HasIndex(r => r.DispatchAt)
            .HasDatabaseName("ix_delivery_requests_dispatch_at");

        builder.HasIndex(r => r.ExternalTaskId)
            .HasDatabaseName("ix_delivery_requests_external_task_id");
    }
}