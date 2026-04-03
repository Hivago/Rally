using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RallyAPI.Catalog.Domain.MenuItems;

namespace RallyAPI.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemOptionConfiguration : IEntityTypeConfiguration<MenuItemOption>
{
    public void Configure(EntityTypeBuilder<MenuItemOption> builder)
    {
        builder.ToTable("menu_item_options");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id");

        builder.Property(o => o.MenuItemId)
            .HasColumnName("menu_item_id")
            .IsRequired();

        builder.Property(o => o.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.AdditionalPrice)
            .HasColumnName("additional_price")
            .HasPrecision(10, 2);

        builder.Property(o => o.IsDefault)
            .HasColumnName("is_default");

        builder.Property(o => o.OptionGroupId)
            .HasColumnName("option_group_id");

        builder.HasIndex(o => o.MenuItemId)
            .HasDatabaseName("ix_menu_item_options_menu_item_id");

        builder.HasIndex(o => o.OptionGroupId)
            .HasDatabaseName("ix_menu_item_options_option_group_id");
    }
}