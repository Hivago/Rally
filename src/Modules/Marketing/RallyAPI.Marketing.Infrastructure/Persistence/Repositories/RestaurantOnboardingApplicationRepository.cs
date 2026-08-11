using Microsoft.EntityFrameworkCore;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.Marketing.Domain.Enums;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Repositories;

internal sealed class RestaurantOnboardingApplicationRepository : IRestaurantOnboardingApplicationRepository
{
    private readonly MarketingDbContext _context;

    public RestaurantOnboardingApplicationRepository(MarketingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RestaurantOnboardingApplication application, CancellationToken cancellationToken = default)
    {
        await _context.RestaurantOnboardingApplications.AddAsync(application, cancellationToken);
    }

    public void Update(RestaurantOnboardingApplication application)
        => _context.RestaurantOnboardingApplications.Update(application);

    public Task<RestaurantOnboardingApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.RestaurantOnboardingApplications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasPendingApplicationAsync(string phone, string email, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = phone.Trim();
        var normalizedEmail = email.Trim();
        return _context.RestaurantOnboardingApplications
            .AsNoTracking()
            .AnyAsync(x =>
                x.Status == OnboardingApplicationStatus.Pending &&
                (x.Phone == normalizedPhone || x.Email == normalizedEmail),
                cancellationToken);
    }

    public async Task<(IReadOnlyList<RestaurantOnboardingApplication> Items, int Total)> GetPagedAsync(
        OnboardingApplicationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RestaurantOnboardingApplications.AsNoTracking();

        if (status is not null)
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.RestaurantName, pattern) ||
                EF.Functions.ILike(x.OwnerName, pattern) ||
                EF.Functions.ILike(x.Phone, pattern) ||
                EF.Functions.ILike(x.Email, pattern) ||
                EF.Functions.ILike(x.City, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
