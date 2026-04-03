using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Catalog.Domain.MenuItems;

public class MenuItem : AggregateRoot
{
    private readonly List<MenuItemOption> _options = new();
    private readonly List<MenuItemOptionGroup> _optionGroups = new();

    public Guid MenuId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal BasePrice { get; private set; }
    public string? ImageUrl { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool IsVegetarian { get; private set; }
    public int PreparationTimeMinutes { get; private set; }
    public List<string> Tags { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<MenuItemOption> Options => _options.AsReadOnly();
    public IReadOnlyCollection<MenuItemOptionGroup> OptionGroups => _optionGroups.AsReadOnly();

    private MenuItem() { } // EF Core

    public static MenuItem Create(
        Guid menuId,
        Guid restaurantId,
        string name,
        string? description,
        decimal basePrice,
        string? imageUrl,
        int displayOrder,
        bool isVegetarian,
        int preparationTimeMinutes)
    {
        var item = new MenuItem
        {
            Id = Guid.NewGuid(),
            MenuId = menuId,
            RestaurantId = restaurantId,
            Name = name,
            Description = description,
            BasePrice = basePrice,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsAvailable = true,
            IsVegetarian = isVegetarian,
            PreparationTimeMinutes = preparationTimeMinutes,
            CreatedAt = DateTime.UtcNow
        };

        return item;
    }

    public void Update(
        string name,
        string? description,
        decimal basePrice,
        string? imageUrl,
        int displayOrder,
        bool isVegetarian,
        int preparationTimeMinutes)
    {
        Name = name;
        Description = description;
        BasePrice = basePrice;
        ImageUrl = imageUrl;
        DisplayOrder = displayOrder;
        IsVegetarian = isVegetarian;
        PreparationTimeMinutes = preparationTimeMinutes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ToggleAvailability()
    {
        IsAvailable = !IsAvailable;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
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

    public void ClearOptions()
    {
        _options.Clear();
    }

    public void AddOptionGroup(MenuItemOptionGroup group)
    {
        _optionGroups.Add(group);
    }

    public void RemoveOptionGroup(Guid groupId)
    {
        var group = _optionGroups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
            _optionGroups.Remove(group);
    }

    public void ClearOptionGroups()
    {
        _optionGroups.Clear();
    }

    public void SetTags(List<string> tags)
    {
        Tags = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the menu item's image URL after a successful R2 upload confirmation.
    /// Called by ConfirmMenuItemImageCommandHandler after validating the upload.
    /// </summary>
    public void UpdateImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("Image URL cannot be empty.", nameof(imageUrl));

        ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}