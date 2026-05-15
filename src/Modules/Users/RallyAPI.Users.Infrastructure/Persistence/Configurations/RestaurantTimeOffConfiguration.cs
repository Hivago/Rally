using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public sealed class RestaurantTimeOffConfiguration : IEntityTypeConfiguration<RestaurantTimeOff>
{
    public void Configure(EntityTypeBuilder<RestaurantTimeOff> builder)
    {
        builder.ToTable("restaurant_time_offs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(t => t.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(t => t.EndsAt)
            .HasColumnName("ends_at")
            .IsRequired();

        builder.Property(t => t.Reason)
            .HasColumnName("reason")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(t => t.CreatedByOwnerId)
            .HasColumnName("created_by_owner_id")
            .IsRequired();

        builder.Property(t => t.CancelledAt)
            .HasColumnName("cancelled_at")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        // FK to restaurants
        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(t => t.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hot path: "is this outlet currently closed?" — restaurant_id + ends_at filtered by cancelled
        builder.HasIndex(t => new { t.RestaurantId, t.EndsAt })
            .HasDatabaseName("ix_restaurant_time_offs_restaurant_ends_at");

        // For owner listing — restaurant_id + starts_at
        builder.HasIndex(t => new { t.RestaurantId, t.StartsAt })
            .HasDatabaseName("ix_restaurant_time_offs_restaurant_starts_at");

        builder.HasQueryFilter(t => t.DeletedAt == null);

        builder.Ignore(t => t.DomainEvents);
    }
}
