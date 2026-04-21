using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public sealed class RestaurantScheduleSlotConfiguration : IEntityTypeConfiguration<RestaurantScheduleSlot>
{
    public void Configure(EntityTypeBuilder<RestaurantScheduleSlot> builder)
    {
        builder.ToTable("restaurant_schedule_slots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(s => s.DayOfWeek)
            .HasColumnName("day_of_week")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.OpensAt)
            .HasColumnName("opens_at")
            .IsRequired();

        builder.Property(s => s.ClosesAt)
            .HasColumnName("closes_at")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        builder.HasIndex(s => new { s.RestaurantId, s.DayOfWeek })
            .HasDatabaseName("idx_restaurant_schedule_slots_restaurant_day");

        builder.Ignore(s => s.DomainEvents);
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
