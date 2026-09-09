using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public sealed class RestaurantLocationReviewQueueRepository : IRestaurantLocationReviewQueueRepository
{
    private readonly UsersDbContext _context;

    public RestaurantLocationReviewQueueRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<RestaurantLocationReviewQueue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.RestaurantLocationReviewQueue.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public Task<bool> HasPendingAsync(Guid restaurantId, CancellationToken cancellationToken = default)
        => _context.RestaurantLocationReviewQueue
            .AsNoTracking()
            .AnyAsync(q => q.RestaurantId == restaurantId
                           && q.Status == RestaurantLocationReviewStatus.Pending,
                cancellationToken);

    public async Task<IReadOnlyList<RestaurantLocationReviewQueue>> GetByStatusAsync(
        RestaurantLocationReviewStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => await _context.RestaurantLocationReviewQueue
            .AsNoTracking()
            .Where(q => q.Status == status)
            .OrderByDescending(q => q.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RestaurantLocationReviewQueue entity, CancellationToken cancellationToken = default)
        => await _context.RestaurantLocationReviewQueue.AddAsync(entity, cancellationToken);

    public void Update(RestaurantLocationReviewQueue entity)
        => _context.RestaurantLocationReviewQueue.Update(entity);
}
