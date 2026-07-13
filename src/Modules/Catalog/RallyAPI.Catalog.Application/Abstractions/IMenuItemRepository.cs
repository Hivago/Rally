using RallyAPI.Catalog.Domain.MenuItems;

namespace RallyAPI.Catalog.Application.Abstractions;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MenuItem?> GetByIdWithOptionsAsync(Guid id, CancellationToken ct = default);
    Task<List<MenuItem>> GetByMenuIdAsync(Guid menuId, CancellationToken ct = default);
    Task<List<MenuItem>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default);
    Task<List<MenuItem>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default);
    Task<MenuItem?> GetByOptionGroupIdAsync(Guid optionGroupId, CancellationToken ct = default);
    Task<MenuItem?> GetByOptionIdAsync(Guid optionId, CancellationToken ct = default);
    void Add(MenuItem item);
    void Update(MenuItem item, CancellationToken ct = default);
    void Delete(MenuItem item);

    // Explicitly mark newly-created child entities as Added. Client-set Guid
    // keys on a tracked aggregate's navigation are otherwise misdetected as
    // Modified (→ UPDATE affecting 0 rows), so new options/groups must be
    // added through the context directly.
    void AddOption(MenuItemOption option);
    void AddOptionGroup(MenuItemOptionGroup group);
}