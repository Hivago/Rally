using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Catalog.Domain.MenuItems;

namespace RallyAPI.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemOptionGroupConfiguration : IEntityTypeConfiguration<MenuItemOptionGroup>
{
    public void Configure(EntityTypeBuilder<MenuItemOptionGroup> builder)
    {
        builder.ToTable("menu_item_option_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("id");

        builder.Property(g => g.MenuItemId)
            .HasColumnName("menu_item_id")
            .IsRequired();

        builder.Property(g => g.GroupName)
            .HasColumnName("group_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.IsRequired)
            .HasColumnName("is_required");

        builder.Property(g => g.MinSelections)
            .HasColumnName("min_selections");

        builder.Property(g => g.MaxSelections)
            .HasColumnName("max_selections");

        builder.Property(g => g.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(g => g.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(g => g.Options)
            .WithOne()
            .HasForeignKey(o => o.OptionGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.MenuItemId)
            .HasDatabaseName("ix_menu_item_option_groups_menu_item_id");
    }
}
