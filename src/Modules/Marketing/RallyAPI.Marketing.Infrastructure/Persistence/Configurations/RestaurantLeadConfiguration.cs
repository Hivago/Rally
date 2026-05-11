using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Configurations;

public sealed class RestaurantLeadConfiguration : IEntityTypeConfiguration<RestaurantLead>
{
    public void Configure(EntityTypeBuilder<RestaurantLead> builder)
    {
        builder.ToTable("restaurant_leads");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RestaurantName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DailyOrders).IsRequired();

        builder.Property(x => x.Source).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(45);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Phone).IsUnique();
        builder.HasIndex(x => x.City);
        builder.HasIndex(x => x.CreatedAt);
    }
}
