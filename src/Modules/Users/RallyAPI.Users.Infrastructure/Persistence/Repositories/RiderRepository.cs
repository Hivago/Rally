using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public class RiderRepository : IRiderRepository
{
    private readonly UsersDbContext _context;

    public RiderRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<Rider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Riders
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Rider?> GetByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default)
    {
        return await _context.Riders
            .FirstOrDefaultAsync(r => r.Phone == phone, cancellationToken);
    }

    public async Task<bool> ExistsByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default)
    {
        return await _context.Riders
            .AnyAsync(r => r.Phone == phone, cancellationToken);
    }

    public async Task<List<Rider>> GetOnlineRidersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Riders
            .Where(r => r.IsOnline && r.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Rider?> GetByIdWithKycAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Riders
            .Include(r => r.KycDocuments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(bool? isOnline = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Riders.AsQueryable();
        if (isOnline.HasValue)
            query = query.Where(r => r.IsOnline == isOnline.Value && r.IsActive);
        return await query.CountAsync(cancellationToken);
    }

    public Task<int> CountPendingKycAsync(CancellationToken cancellationToken = default)
        => _context.Riders.CountAsync(r => r.KycStatus == Domain.Enums.KycStatus.Pending, cancellationToken);

    public async Task<(List<Rider> Items, int TotalCount)> GetPagedAsync(
        bool? isOnline,
        KycStatus? kycStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Riders.AsQueryable();

        if (isOnline.HasValue)
            query = query.Where(r => r.IsOnline == isOnline.Value);

        if (kycStatus.HasValue)
            query = query.Where(r => r.KycStatus == kycStatus.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Rider rider, CancellationToken cancellationToken = default)
    {
        await _context.Riders.AddAsync(rider, cancellationToken);
    }

    public void Update(Rider rider, CancellationToken cancellationToken = default)
    {
        _context.Riders.Update(rider);
    }

}