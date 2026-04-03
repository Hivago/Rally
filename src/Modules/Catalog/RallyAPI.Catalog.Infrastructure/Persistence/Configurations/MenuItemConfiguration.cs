using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Catalog.Domain.MenuItems;

namespace RallyAPI.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.MenuId)
            .HasColumnName("menu_id")
            .IsRequired();

        builder.Property(m => m.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(m => m.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(m => m.BasePrice)
            .HasColumnName("base_price")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(m => m.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(m => m.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(m => m.IsAvailable)
            .HasColumnName("is_available");

        builder.Property(m => m.IsVegetarian)
            .HasColumnName("is_vegetarian");

        builder.Property(m => m.PreparationTimeMinutes)
            .HasColumnName("preparation_time_minutes");

        builder.Property(m => m.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(m => m.Options)
            .WithOne()
            .HasForeignKey(o => o.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.OptionGroups)
            .WithOne()
            .HasForeignKey(g => g.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.MenuId)
            .HasDatabaseName("ix_menu_items_menu_id");

        builder.HasIndex(m => m.RestaurantId)
            .HasDatabaseName("ix_menu_items_restaurant_id");
    }
}