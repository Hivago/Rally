using Microsoft.EntityFrameworkCore;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Infrastructure.Persistence.Repositories;

public sealed class RiderPayoutLedgerRepository : IRiderPayoutLedgerRepository
{
    private readonly UsersDbContext _context;

    public RiderPayoutLedgerRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<RiderPayoutLedger?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.RiderPayoutLedger.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<RiderPayoutLedger?> GetByCycleAsync(
        Guid riderId,
        DateTime cycleStartUtc,
        DateTime cycleEndUtc,
        CancellationToken ct = default)
        => _context.RiderPayoutLedger.FirstOrDefaultAsync(p =>
            p.RiderId == riderId
            && p.CycleStartUtc == cycleStartUtc
            && p.CycleEndUtc == cycleEndUtc,
            ct);

    public async Task AddAsync(RiderPayoutLedger payout, CancellationToken ct = default)
        => await _context.RiderPayoutLedger.AddAsync(payout, ct);

    public void Update(RiderPayoutLedger payout)
        => _context.RiderPayoutLedger.Update(payout);
}
