using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public class RestaurantRepository : IRestaurantRepository
{
    private readonly UsersDbContext _context;

    public RestaurantRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<Restaurant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Restaurant?> GetByIdWithScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants
            .Include(r => r.ScheduleSlots)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Restaurant?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants
            .FirstOrDefaultAsync(r => r.Email == email, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants
            .AnyAsync(r => r.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<Restaurant>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants
            .AsNoTracking()
            .Where(r => r.OwnerId == ownerId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetOutletCountsByOwnerIdsAsync(
        IReadOnlyCollection<Guid> ownerIds,
        CancellationToken cancellationToken = default)
    {
        if (ownerIds.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _context.Restaurants
            .AsNoTracking()
            .Where(r => r.OwnerId != null && ownerIds.Contains(r.OwnerId.Value))
            .GroupBy(r => r.OwnerId!.Value)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.OwnerId, x => x.Count);
    }

    public async Task AddAsync(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        await _context.Restaurants.AddAsync(restaurant, cancellationToken);
    }

    public void Update(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        _context.Restaurants.Update(restaurant);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Restaurants.CountAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
        => _context.Restaurants.CountAsync(r => r.IsActive, cancellationToken);

    public async Task<(IReadOnlyList<Restaurant> Items, int TotalCount)> GetPagedAsync(
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Restaurants.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}