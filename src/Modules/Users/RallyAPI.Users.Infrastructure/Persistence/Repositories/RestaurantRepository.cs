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
}