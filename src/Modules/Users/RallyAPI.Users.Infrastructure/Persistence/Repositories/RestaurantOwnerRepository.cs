using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public class RestaurantOwnerRepository : IRestaurantOwnerRepository
{
    private readonly UsersDbContext _context;

    public RestaurantOwnerRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<RestaurantOwner?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.RestaurantOwners
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<RestaurantOwner?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        return await _context.RestaurantOwners
            .FirstOrDefaultAsync(o => o.Email == email, ct);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default)
    {
        return await _context.RestaurantOwners
            .AnyAsync(o => o.Email == email, ct);
    }

    public async Task<(IReadOnlyList<RestaurantOwner> Items, int TotalCount)> GetPagedAsync(
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.RestaurantOwners.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(o => o.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.Name.ToLower().Contains(term) ||
                o.Email.Value.ToLower().Contains(term) ||
                o.Phone.Value.Contains(term));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(RestaurantOwner owner, CancellationToken ct = default)
    {
        await _context.RestaurantOwners.AddAsync(owner, ct);
    }

    public void Update(RestaurantOwner owner)
    {
        _context.RestaurantOwners.Update(owner);
    }
}
