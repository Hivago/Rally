using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Entities;

namespace RallyAPI.Orders.Infrastructure;

/// <summary>
/// DbContext for Orders module.
/// Follows modular monolith pattern - separate context per module.
/// </summary>
public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<DeliveryInfo> DeliveryInfos => Set<DeliveryInfo>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutLedger> PayoutLedgers => Set<PayoutLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        // Set default schema for orders module
        modelBuilder.HasDefaultSchema("orders");
    }
}
