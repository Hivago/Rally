using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;

namespace RallyAPI.Delivery.Infrastructure.Repositories;

public sealed class IgmTicketRepository : IIgmTicketRepository
{
    private readonly DeliveryDbContext _dbContext;

    public IgmTicketRepository(DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IgmTicket?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.IgmTickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<IgmTicket?> GetByExternalIssueIdAsync(string externalIssueId, CancellationToken ct = default) =>
        _dbContext.IgmTickets.FirstOrDefaultAsync(t => t.ExternalIssueId == externalIssueId, ct);

    public async Task<IReadOnlyList<IgmTicket>> GetByDeliveryRequestIdAsync(Guid deliveryRequestId, CancellationToken ct = default) =>
        await _dbContext.IgmTickets
            .Where(t => t.DeliveryRequestId == deliveryRequestId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<IgmTicket>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await _dbContext.IgmTickets
            .Where(t => t.OrderId == orderId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<IgmTicket>> GetByStateAsync(IgmTicketState state, CancellationToken ct = default) =>
        await _dbContext.IgmTickets
            .Where(t => t.State == state)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(IgmTicket ticket, CancellationToken ct = default)
    {
        await _dbContext.IgmTickets.AddAsync(ticket, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IgmTicket ticket, CancellationToken ct = default)
    {
        _dbContext.IgmTickets.Update(ticket);
        await _dbContext.SaveChangesAsync(ct);
    }
}
