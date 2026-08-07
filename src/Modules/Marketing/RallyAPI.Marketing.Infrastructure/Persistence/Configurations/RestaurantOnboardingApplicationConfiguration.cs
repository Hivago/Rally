using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOnboardingApplicationConfiguration : IEntityTypeConfiguration<RestaurantOnboardingApplication>
{
    public void Configure(EntityTypeBuilder<RestaurantOnboardingApplication> builder)
    {
        builder.ToTable("restaurant_onboarding_applications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RestaurantName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AddressLine).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CuisineType).HasMaxLength(200);
        builder.Property(x => x.FssaiNumber).HasMaxLength(20);

        // Ciphertext columns are intentionally unbounded (`text`, no HasMaxLength) — AES-GCM
        // output (nonce + ciphertext + tag, base64) is longer than the plaintext and there's
        // no reason to guess a cap. A HasMaxLength here that's too tight is exactly the class
        // of bug that broke the payout reconcile manual-resolve marker (see
        // docs/icici-payout-reconciliation-rules.md) — don't repeat it on financial PII.
        builder.Property(x => x.BankAccountNumberEncrypted).IsRequired();
        builder.Property(x => x.BankAccountLast4).IsRequired().HasMaxLength(4);
        builder.Property(x => x.BankIfscCode).IsRequired().HasMaxLength(11);
        builder.Property(x => x.BankAccountName).IsRequired().HasMaxLength(255);

        builder.Property(x => x.PanNumberEncrypted).IsRequired();
        builder.Property(x => x.PanLast4).IsRequired().HasMaxLength(4);

        builder.Property(x => x.GstNumberEncrypted);
        builder.Property(x => x.GstLast4).HasMaxLength(4);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ReviewedByAdminId);
        builder.Property(x => x.ReviewedAtUtc);
        builder.Property(x => x.ReviewNotes).HasMaxLength(2000);

        builder.Property(x => x.Source).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(45);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Phone);
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.CreatedAt);
    }
}
