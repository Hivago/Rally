using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RallyAPI.Users.Infrastructure.Persistence.Configurations
{
    public class RiderConfiguration : IEntityTypeConfiguration<Rider>
    {
        public void Configure(EntityTypeBuilder<Rider> builder)
        {
            // Table mapping
            builder.ToTable("riders");

            // Primary Key
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            // Phone - Value Object conversion
            builder.Property(r => r.Phone)
                .HasConversion(
                    phone => phone.Value,
                    value => PhoneNumber.Create(value).Value
                )
                .HasColumnName("phone")
                .HasMaxLength(15)
                .IsRequired();

            // Name
            builder.Property(r => r.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            // Email - Value Object conversion (nullable)
            builder.Property(r => r.Email)
                .HasConversion(
                    email => email == null ? null : email.Value,
                    value => value == null ? null : Email.Create(value).Value
                )
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired(false);

            // VehicleType - Enum stored as string
            builder.Property(r => r.VehicleType)
                .HasConversion<string>()
                .HasColumnName("vehicle_type")
                .HasMaxLength(20)
                .IsRequired();

            // VehicleNumber
            builder.Property(r => r.VehicleNumber)
                .HasColumnName("vehicle_number")
                .HasMaxLength(20)
                .IsRequired(false);

            // KycStatus - Enum stored as string
            builder.Property(r => r.KycStatus)
                .HasConversion<string>()
                .HasColumnName("kyc_status")
                .HasMaxLength(20)
                .HasDefaultValue(KycStatus.Pending)
                .IsRequired();

            // IsActive
            builder.Property(r => r.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .IsRequired();

            // IsOnline
            builder.Property(r => r.IsOnline)
                .HasColumnName("is_online")
                .HasDefaultValue(false)
                .IsRequired();

            // Bank details (nullable — set by rider for payout)
            builder.Property(r => r.BankAccountNumber)
                .HasColumnName("bank_account_number")
                .HasMaxLength(20)
                .IsRequired(false);

            builder.Property(r => r.BankIfscCode)
                .HasColumnName("bank_ifsc_code")
                .HasMaxLength(11)
                .IsRequired(false);

            builder.Property(r => r.BankAccountName)
                .HasColumnName("bank_account_name")
                .HasMaxLength(255)
                .IsRequired(false);

            // Location tracking
            builder.Property(r => r.CurrentLatitude)
                .HasColumnName("current_latitude")
                .HasPrecision(10, 8)
                .IsRequired(false);

            builder.Property(r => r.CurrentLongitude)
                .HasColumnName("current_longitude")
                .HasPrecision(11, 8)
                .IsRequired(false);

            builder.Property(r => r.LastLocationUpdate)
                .HasColumnName("last_location_update")
                .IsRequired(false);

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
            builder.HasIndex(r => r.Phone)
                .IsUnique()
                .HasDatabaseName("idx_riders_phone");

            builder.HasIndex(r => new { r.IsOnline, r.IsActive })
                .HasDatabaseName("idx_riders_online");

            builder.HasIndex(r => new { r.CurrentLatitude, r.CurrentLongitude })
                .HasDatabaseName("idx_riders_location");

            // Query Filter - Soft delete
            builder.HasQueryFilter(r => r.DeletedAt == null);

            // Ignore domain events
            builder.Ignore(r => r.DomainEvents);

            builder.Property(r => r.CurrentDeliveryId)
    .HasColumnName("current_delivery_id")
    .IsRequired(false);

            builder.Property(r => r.CurrentDeliveryAssignedAt)
                .HasColumnName("current_delivery_assigned_at")
                .IsRequired(false);

            // Add index for finding available riders
            builder.HasIndex(r => new { r.IsOnline, r.IsActive, r.CurrentDeliveryId })
                .HasDatabaseName("ix_riders_availability");

            // Add index for location-based queries
            builder.HasIndex(r => new { r.CurrentLatitude, r.CurrentLongitude })
                .HasDatabaseName("ix_riders_location")
                .HasFilter("current_latitude IS NOT NULL AND current_longitude IS NOT NULL");
        }
    }
}
