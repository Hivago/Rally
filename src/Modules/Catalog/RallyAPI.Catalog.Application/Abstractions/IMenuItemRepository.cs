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
}