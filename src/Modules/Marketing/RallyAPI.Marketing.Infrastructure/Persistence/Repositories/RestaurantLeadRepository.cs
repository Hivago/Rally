using Microsoft.EntityFrameworkCore;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Repositories;

internal sealed class RestaurantLeadRepository : IRestaurantLeadRepository
{
    private readonly MarketingDbContext _context;

    public RestaurantLeadRepository(MarketingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RestaurantLead lead, CancellationToken cancellationToken = default)
    {
        await _context.RestaurantLeads.AddAsync(lead, cancellationToken);
    }

    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var normalized = phone.Trim();
        return _context.RestaurantLeads
            .AsNoTracking()
            .AnyAsync(x => x.Phone == normalized, cancellationToken);
    }

    public async Task<(IReadOnlyList<RestaurantLead> Items, int Total)> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RestaurantLeads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.RestaurantName, pattern) ||
                EF.Functions.ILike(x.OwnerName, pattern) ||
                EF.Functions.ILike(x.Phone, pattern) ||
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

    public async Task<IReadOnlyList<RestaurantLead>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RestaurantLeads
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
