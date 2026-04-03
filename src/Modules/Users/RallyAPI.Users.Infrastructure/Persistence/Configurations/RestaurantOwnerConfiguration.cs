using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public class RestaurantOwnerConfiguration : IEntityTypeConfiguration<RestaurantOwner>
{
    public void Configure(EntityTypeBuilder<RestaurantOwner> builder)
    {
        builder.ToTable("restaurant_owners");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value).Value)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.Phone)
            .HasConversion(
                phone => phone.Value,
                value => PhoneNumber.Create(value).Value)
            .HasColumnName("phone")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(o => o.PanNumber)
            .HasColumnName("pan_number")
            .HasMaxLength(10);

        builder.Property(o => o.GstNumber)
            .HasColumnName("gst_number")
            .HasMaxLength(15);

        builder.Property(o => o.BankAccountNumber)
            .HasColumnName("bank_account_number")
            .HasMaxLength(20);

        builder.Property(o => o.BankIfscCode)
            .HasColumnName("bank_ifsc_code")
            .HasMaxLength(11);

        builder.Property(o => o.BankAccountName)
            .HasColumnName("bank_account_name")
            .HasMaxLength(255);

        builder.Property(o => o.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(o => o.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        builder.Property(o => o.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        // Indexes
        builder.HasIndex(o => o.Email)
            .IsUnique()
            .HasDatabaseName("idx_restaurant_owners_email");

        // Query Filter - Soft delete
        builder.HasQueryFilter(o => o.DeletedAt == null);

        // Ignore domain events
        builder.Ignore(o => o.DomainEvents);
    }
}
