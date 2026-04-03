using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        // Table mapping
        builder.ToTable("restaurants");

        // Primary Key
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Name
        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // Phone - Value Object conversion
        builder.Property(r => r.Phone)
            .HasConversion(
                phone => phone.Value,
                value => PhoneNumber.Create(value).Value
            )
            .HasColumnName("phone")
            .HasMaxLength(15)
            .IsRequired();

        // Email - Value Object conversion
        builder.Property(r => r.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value).Value
            )
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        // Password Hash
        builder.Property(r => r.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        // Address - Direct properties (NOT Value Object)
        builder.Property(r => r.AddressLine)
            .HasColumnName("address_line")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(10, 8)
            .IsRequired();

        builder.Property(r => r.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(11, 8)
            .IsRequired();

        // IsActive
        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // IsAcceptingOrders
        builder.Property(r => r.IsAcceptingOrders)
            .HasColumnName("is_accepting_orders")
            .HasDefaultValue(false)
            .IsRequired();

        // AutoAcceptOrders
        builder.Property(r => r.AutoAcceptOrders)
            .HasColumnName("auto_accept_orders")
            .HasDefaultValue(false)
            .IsRequired();

        // AvgPrepTimeMins
        builder.Property(r => r.AvgPrepTimeMins)
            .HasColumnName("avg_prep_time_mins")
            .HasDefaultValue(20)
            .IsRequired();

        // Business Hours - TimeOnly
        builder.Property(r => r.OpeningTime)
            .HasColumnName("opening_time")
            .IsRequired();

        builder.Property(r => r.ClosingTime)
            .HasColumnName("closing_time")
            .IsRequired();

        // Commission
        builder.Property(r => r.CommissionPercentage)
            .HasColumnName("commission_percentage")
            .HasPrecision(5, 2)
            .HasDefaultValue(20.00m)
            .IsRequired();

        // Base Entity properties
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        // Version for optimistic concurrency
        builder.Property(r => r.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        // Indexes
        builder.HasIndex(r => r.Email)
            .IsUnique()
            .HasDatabaseName("idx_restaurants_email");

        builder.HasIndex(r => new { r.Latitude, r.Longitude })
            .HasDatabaseName("idx_restaurants_location");

        builder.HasIndex(r => new { r.IsActive, r.IsAcceptingOrders })
            .HasDatabaseName("idx_restaurants_active");

        // Query Filter - Soft delete
        builder.HasQueryFilter(r => r.DeletedAt == null);

        // Ignore domain events
        builder.Ignore(r => r.DomainEvents);


        builder.Property(e => e.LogoUrl)
       .HasColumnName("logo_url")
       .HasMaxLength(500);

        builder.Property(e => e.LogoFileKey)
               .HasColumnName("logo_file_key")
               .HasMaxLength(500);

        // Owner link (multi-outlet support)
        builder.Property(r => r.OwnerId)
            .HasColumnName("owner_id");

        builder.HasOne<RestaurantOwner>()
            .WithMany()
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // FSSAI compliance
        builder.Property(r => r.FssaiNumber)
            .HasColumnName("fssai_number")
            .HasMaxLength(20);

        // Cuisine/dietary attributes
        builder.Property(r => r.CuisineTypes)
            .HasColumnName("cuisine_types")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(r => r.IsPureVeg)
            .HasColumnName("is_pure_veg")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.IsVeganFriendly)
            .HasColumnName("is_vegan_friendly")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.HasJainOptions)
            .HasColumnName("has_jain_options")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.MinOrderAmount)
            .HasColumnName("min_order_amount")
            .HasPrecision(10, 2)
            .HasDefaultValue(0m)
            .IsRequired();
    }
}