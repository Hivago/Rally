using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Infrastructure.Persistence;

namespace RallyAPI.Delivery.Infrastructure.Repositories;

public sealed class DeliveryQuoteRepository : IDeliveryQuoteRepository
{
    private readonly DeliveryDbContext _dbContext;

    public DeliveryQuoteRepository(DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeliveryQuote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Quotes
            .FirstOrDefaultAsync(q => q.Id == id, ct);
    }

    public async Task AddAsync(DeliveryQuote quote, CancellationToken ct = default)
    {
        await _dbContext.Quotes.AddAsync(quote, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DeliveryQuote quote, CancellationToken ct = default)
    {
        _dbContext.Quotes.Update(quote);
        await _dbContext.SaveChangesAsync(ct);
    }
}
