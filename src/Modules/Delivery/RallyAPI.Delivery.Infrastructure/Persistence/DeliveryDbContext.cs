using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Domain.Entities;

namespace RallyAPI.Delivery.Infrastructure.Persistence;

public sealed class DeliveryDbContext : DbContext
{
    public const string Schema = "delivery";

    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options)
    {
    }

    public DbSet<DeliveryQuote> Quotes => Set<DeliveryQuote>();
    public DbSet<DeliveryRequest> DeliveryRequests => Set<DeliveryRequest>();
    public DbSet<RiderOffer> RiderOffers => Set<RiderOffer>();
    public DbSet<IgmTicket> IgmTickets => Set<IgmTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}