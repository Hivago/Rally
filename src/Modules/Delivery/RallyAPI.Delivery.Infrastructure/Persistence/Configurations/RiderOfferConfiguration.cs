using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;

namespace RallyAPI.Delivery.Infrastructure.Persistence.Configurations;

public sealed class RiderOfferConfiguration : IEntityTypeConfiguration<RiderOffer>
{
    public void Configure(EntityTypeBuilder<RiderOffer> builder)
    {
        builder.ToTable("rider_offers");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.DeliveryRequestId)
            .HasColumnName("delivery_request_id")
            .IsRequired();

        builder.Property(o => o.RiderId)
            .HasColumnName("rider_id")
            .IsRequired();

        // Offer Details
        builder.Property(o => o.OfferedAt)
            .HasColumnName("offered_at")
            .IsRequired();

        builder.Property(o => o.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(o => o.Earnings)
            .HasColumnName("earnings")
            .HasPrecision(10, 2)
            .IsRequired();

        // Response
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.RespondedAt)
            .HasColumnName("responded_at");

        builder.Property(o => o.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(200);

        // Rider Snapshot
        builder.Property(o => o.RiderLatitude)
            .HasColumnName("rider_latitude");

        builder.Property(o => o.RiderLongitude)
            .HasColumnName("rider_longitude");

        builder.Property(o => o.DistanceToRestaurantKm)
            .HasColumnName("distance_to_restaurant_km")
            .HasPrecision(8, 2);

        // Notification Tracking
        builder.Property(o => o.NotificationSentAt)
            .HasColumnName("notification_sent_at");

        builder.Property(o => o.NotificationDelivered)
            .HasColumnName("notification_delivered")
            .HasDefaultValue(false)
            .IsRequired();

        // Indexes
        builder.HasIndex(o => o.DeliveryRequestId)
            .HasDatabaseName("ix_rider_offers_delivery_request_id");

        builder.HasIndex(o => o.RiderId)
            .HasDatabaseName("ix_rider_offers_rider_id");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_rider_offers_status");

        builder.HasIndex(o => new { o.RiderId, o.Status })
            .HasDatabaseName("ix_rider_offers_rider_status");
    }
}