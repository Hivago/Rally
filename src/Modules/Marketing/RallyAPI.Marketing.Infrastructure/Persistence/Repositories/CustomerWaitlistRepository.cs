using Microsoft.EntityFrameworkCore;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence.Repositories;

internal sealed class CustomerWaitlistRepository : ICustomerWaitlistRepository
{
    private readonly MarketingDbContext _context;

    public CustomerWaitlistRepository(MarketingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerWaitlistEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.CustomerWaitlistEntries.AddAsync(entry, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _context.CustomerWaitlistEntries
            .AsNoTracking()
            .AnyAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var normalized = phone.Trim();
        return _context.CustomerWaitlistEntries
            .AsNoTracking()
            .AnyAsync(x => x.Phone == normalized, cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerWaitlistEntry> Items, int Total)> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CustomerWaitlistEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Email, pattern) ||
                EF.Functions.ILike(x.Phone, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<CustomerWaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CustomerWaitlistEntries
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
