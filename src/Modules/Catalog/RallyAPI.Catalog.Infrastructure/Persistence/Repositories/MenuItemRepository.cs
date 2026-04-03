using Microsoft.EntityFrameworkCore;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.Catalog.Domain.MenuItems;

namespace RallyAPI.Catalog.Infrastructure.Persistence.Repositories;

internal sealed class MenuItemRepository : IMenuItemRepository
{
    private readonly CatalogDbContext _context;

    public MenuItemRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<MenuItem?> GetByIdWithOptionsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MenuItems
            .Include(m => m.Options)
            .Include(m => m.OptionGroups)
                .ThenInclude(g => g.Options)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<List<MenuItem>> GetByMenuIdAsync(Guid menuId, CancellationToken ct = default)
    {
        return await _context.MenuItems
            .Include(m => m.Options)
            .Include(m => m.OptionGroups)
                .ThenInclude(g => g.Options)
            .Where(m => m.MenuId == menuId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<List<MenuItem>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct = default)
    {
        return await _context.MenuItems
            .Include(m => m.Options)
            .Include(m => m.OptionGroups)
                .ThenInclude(g => g.Options)
            .Where(m => m.RestaurantId == restaurantId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<List<MenuItem>> SearchAsync(string query, int maxResults = 20, CancellationToken ct = default)
    {
        var searchTerm = $"%{query}%";

        return await _context.MenuItems
            .Include(m => m.Options)
            .Include(m => m.OptionGroups)
                .ThenInclude(g => g.Options)
            .Where(m => m.IsAvailable &&
                (EF.Functions.ILike(m.Name, searchTerm) ||
                 (m.Description != null && EF.Functions.ILike(m.Description, searchTerm))))
            .OrderBy(m => m.Name)
            .Take(maxResults)
            .ToListAsync(ct);
    }


    public void Add(MenuItem item) => _context.MenuItems.Add(item);

    public void Update(MenuItem item, CancellationToken ct = default) => _context.MenuItems.Update(item);

    public void Delete(MenuItem item) => _context.MenuItems.Remove(item);
}