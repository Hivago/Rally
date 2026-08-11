using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public sealed class RiderPayoutExportBatchRepository : IRiderPayoutExportBatchRepository
{
    private readonly UsersDbContext _context;

    public RiderPayoutExportBatchRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<RiderPayoutExportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RiderPayoutExportBatches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task AddAsync(RiderPayoutExportBatch batch, CancellationToken ct = default)
        => await _context.RiderPayoutExportBatches.AddAsync(batch, ct);

    public void Update(RiderPayoutExportBatch batch)
        => _context.RiderPayoutExportBatches.Update(batch);

    public async Task<IReadOnlyList<RiderPayoutExportBatch>> GetRecentAsync(
        PayoutExportBatchStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.RiderPayoutExportBatches.AsQueryable();
        if (status is not null)
            query = query.Where(b => b.Status == status);

        return await query
            .OrderByDescending(b => b.GeneratedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
