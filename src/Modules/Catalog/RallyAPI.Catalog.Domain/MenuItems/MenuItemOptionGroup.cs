using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Catalog.Domain.MenuItems;

/// <summary>
/// Groups related options for a menu item (e.g., "Choose Size", "Add Toppings").
/// IsRequired=true means customer must select at least MinSelections options.
/// </summary>
public class MenuItemOptionGroup : BaseEntity
{
    private readonly List<MenuItemOption> _options = new();

    public Guid MenuItemId { get; private set; }
    public string GroupName { get; private set; } = null!;
    public bool IsRequired { get; private set; }
    public int MinSelections { get; private set; }
    public int MaxSelections { get; private set; }
    public int DisplayOrder { get; private set; }

    public IReadOnlyCollection<MenuItemOption> Options => _options.AsReadOnly();

    private MenuItemOptionGroup() { } // EF Core

    public static MenuItemOptionGroup Create(
        Guid menuItemId,
        string groupName,
        bool isRequired,
        int minSelections,
        int maxSelections,
        int displayOrder)
    {
        if (menuItemId == Guid.Empty)
            throw new ArgumentException("Menu item ID is required.", nameof(menuItemId));

        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("Group name is required.", nameof(groupName));

        if (minSelections < 0)
            throw new ArgumentException("Min selections cannot be negative.", nameof(minSelections));

        if (maxSelections < minSelections)
            throw new ArgumentException("Max selections must be >= min selections.", nameof(maxSelections));

        return new MenuItemOptionGroup
        {
            Id = Guid.NewGuid(),
            MenuItemId = menuItemId,
            GroupName = groupName.Trim(),
            IsRequired = isRequired,
            MinSelections = minSelections,
            MaxSelections = maxSelections,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string groupName, bool isRequired, int minSelections, int maxSelections, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("Group name is required.", nameof(groupName));

        if (maxSelections < minSelections)
            throw new ArgumentException("Max selections must be >= min selections.", nameof(maxSelections));

        GroupName = groupName.Trim();
        IsRequired = isRequired;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddOption(MenuItemOption option)
    {
        _options.Add(option);
    }

    public void RemoveOption(Guid optionId)
    {
        var option = _options.FirstOrDefault(o => o.Id == optionId);
        if (option != null)
            _options.Remove(option);
    }
}
