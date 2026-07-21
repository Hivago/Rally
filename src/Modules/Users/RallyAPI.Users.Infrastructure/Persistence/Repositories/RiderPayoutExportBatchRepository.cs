using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

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
}
