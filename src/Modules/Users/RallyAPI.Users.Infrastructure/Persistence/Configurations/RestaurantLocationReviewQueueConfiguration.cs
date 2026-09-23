using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public sealed class RestaurantLocationReviewQueueConfiguration : IEntityTypeConfiguration<RestaurantLocationReviewQueue>
{
    public void Configure(EntityTypeBuilder<RestaurantLocationReviewQueue> builder)
    {
        builder.ToTable("restaurant_location_review_queue");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(q => q.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(q => q.CurrentLatitude)
            .HasColumnName("current_latitude")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(q => q.CurrentLongitude)
            .HasColumnName("current_longitude")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(q => q.SuggestedLatitude)
            .HasColumnName("suggested_latitude")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(q => q.SuggestedLongitude)
            .HasColumnName("suggested_longitude")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(q => q.AverageDriftMeters)
            .HasColumnName("average_drift_meters")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(q => q.SampleSize)
            .HasColumnName("sample_size")
            .IsRequired();

        builder.Property(q => q.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .HasDefaultValue(RestaurantLocationReviewStatus.Pending)
            .IsRequired();

        builder.Property(q => q.DetectedAt)
            .HasColumnName("detected_at")
            .IsRequired();

        builder.Property(q => q.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(q => q.ReviewedByAdminId)
            .HasColumnName("reviewed_by_admin_id");

        builder.Property(q => q.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(q => q.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(q => q.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        // FK to restaurants
        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(q => q.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Admin queue view: pending findings first
        builder.HasIndex(q => q.Status)
            .HasDatabaseName("ix_restaurant_location_review_queue_status");

        // Dedup check in the drift-detection event handler: does this restaurant
        // already have an open (Pending) finding?
        builder.HasIndex(q => new { q.RestaurantId, q.Status })
            .HasDatabaseName("ix_restaurant_location_review_queue_restaurant_status");

        builder.HasQueryFilter(q => q.DeletedAt == null);

        builder.Ignore(q => q.DomainEvents);
    }
}
