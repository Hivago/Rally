using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public sealed class RestaurantTimeOffRepository : IRestaurantTimeOffRepository
{
    private readonly UsersDbContext _context;

    public RestaurantTimeOffRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<RestaurantTimeOff?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.RestaurantTimeOffs.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RestaurantTimeOff>> GetForRestaurantAsync(
        Guid restaurantId,
        bool includeCancelled,
        bool includePast,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RestaurantTimeOffs
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId);

        if (!includeCancelled)
            query = query.Where(t => t.CancelledAt == null);

        if (!includePast)
            query = query.Where(t => t.EndsAt > nowUtc);

        return await query
            .OrderBy(t => t.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasOverlapAsync(
        Guid restaurantId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RestaurantTimeOffs
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId
                        && t.CancelledAt == null
                        && t.StartsAt < endsAtUtc
                        && t.EndsAt > startsAtUtc);

        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public Task<bool> IsClosedAtAsync(
        Guid restaurantId,
        DateTime momentUtc,
        CancellationToken cancellationToken = default)
        => _context.RestaurantTimeOffs
            .AsNoTracking()
            .AnyAsync(t => t.RestaurantId == restaurantId
                           && t.CancelledAt == null
                           && t.StartsAt <= momentUtc
                           && t.EndsAt > momentUtc,
                cancellationToken);

    public async Task AddAsync(RestaurantTimeOff entity, CancellationToken cancellationToken = default)
        => await _context.RestaurantTimeOffs.AddAsync(entity, cancellationToken);

    public void Update(RestaurantTimeOff entity)
        => _context.RestaurantTimeOffs.Update(entity);
}
