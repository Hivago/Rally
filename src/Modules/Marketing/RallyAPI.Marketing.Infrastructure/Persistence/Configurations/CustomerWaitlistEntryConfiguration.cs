using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Configurations;

public sealed class CustomerWaitlistEntryConfiguration
    : IEntityTypeConfiguration<CustomerWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<CustomerWaitlistEntry> builder)
    {
        builder.ToTable("customer_waitlist");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Source).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Phone).IsUnique();
        builder.HasIndex(x => x.CreatedAt);
    }
}
