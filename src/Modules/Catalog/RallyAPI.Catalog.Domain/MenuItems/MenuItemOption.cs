using RallyAPI.Catalog.Domain.Enums;
using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Catalog.Domain.MenuItems;

public class MenuItemOption : BaseEntity
{
    public Guid MenuItemId { get; private set; }
    public Guid? OptionGroupId { get; private set; }
    public string Name { get; private set; } = null!;
    public OptionType Type { get; private set; }
    public decimal AdditionalPrice { get; private set; }
    public bool IsDefault { get; private set; }

    private MenuItemOption() { } // EF Core

    public static MenuItemOption Create(
        Guid menuItemId,
        string name,
        OptionType type,
        decimal additionalPrice,
        bool isDefault = false,
        Guid? optionGroupId = null)
    {
        return new MenuItemOption
        {
            Id = Guid.NewGuid(),
            MenuItemId = menuItemId,
            OptionGroupId = optionGroupId,
            Name = name,
            Type = type,
            AdditionalPrice = additionalPrice,
            IsDefault = isDefault
        };
    }

    public void SetOptionGroup(Guid? optionGroupId)
    {
        OptionGroupId = optionGroupId;
    }
}