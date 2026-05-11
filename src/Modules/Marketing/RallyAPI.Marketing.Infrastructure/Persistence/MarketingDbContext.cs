using Microsoft.EntityFrameworkCore;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Infrastructure.Persistence;

public class MarketingDbContext : DbContext, IUnitOfWork
{
    public DbSet<CustomerWaitlistEntry> CustomerWaitlistEntries => Set<CustomerWaitlistEntry>();
    public DbSet<RestaurantLead> RestaurantLeads => Set<RestaurantLead>();

    public MarketingDbContext(DbContextOptions<MarketingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("marketing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketingDbContext).Assembly);
    }
}
