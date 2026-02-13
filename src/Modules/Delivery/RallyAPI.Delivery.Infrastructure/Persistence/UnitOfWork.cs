using RallyAPI.Delivery.Domain.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace RallyAPI.Delivery.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly DeliveryDbContext _dbContext;

    public UnitOfWork(DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
