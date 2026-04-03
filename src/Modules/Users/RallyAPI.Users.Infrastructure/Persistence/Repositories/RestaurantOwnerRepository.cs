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

    public async Task AddAsync(RestaurantOwner owner, CancellationToken ct = default)
    {
        await _context.RestaurantOwners.AddAsync(owner, ct);
    }

    public void Update(RestaurantOwner owner)
    {
        _context.RestaurantOwners.Update(owner);
    }
}
