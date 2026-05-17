using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        // Table mapping
        builder.ToTable("admins");

        // Primary Key
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Email - Value Object conversion.
        // Use FromTrusted on read so a legacy row with a non-conforming email
        // cannot crash the materialiser (which is what blew up GET /api/admin/users).
        builder.Property(a => a.Email)
            .HasConversion(
                email => email.Value,
                value => Email.FromTrusted(value)
            )
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        // Password Hash
        builder.Property(a => a.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        // Name
        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        // Role - Enum stored as string, case-insensitive on read with a Support
        // fallback so a row with an unexpected role string cannot crash a list query.
        builder.Property(a => a.Role)
            .HasConversion(
                role => role.ToString(),
                value => ParseRoleSafely(value)
            )
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasDefaultValue(AdminRole.Support)
            .IsRequired();

        // IsActive
        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Base Entity properties
        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        // Version for optimistic concurrency
        builder.Property(a => a.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        // Indexes
        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("idx_admins_email");

        // Query Filter - Soft delete
        builder.HasQueryFilter(a => a.DeletedAt == null);

        // Ignore domain events
        builder.Ignore(a => a.DomainEvents);
    }

    private static AdminRole ParseRoleSafely(string value)
        => Enum.TryParse<AdminRole>(value, ignoreCase: true, out var parsed)
            ? parsed
            : AdminRole.Support;
}