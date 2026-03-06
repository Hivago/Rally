using Microsoft.EntityFrameworkCore;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Order aggregate.
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .FirstOrDefaultAsync(o => o.OrderNumber.Value == orderNumber, cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public void Update(Order order)
    {
        _context.Orders.Update(order);
    }

    public void Remove(Order order)
    {
        _context.Orders.Remove(order);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        Guid customerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(
        Guid restaurantId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByStatusAsync(
        OrderStatus status,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetActiveOrdersByRestaurantAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            OrderStatus.Paid,
            OrderStatus.Confirmed,
            OrderStatus.Preparing,
            OrderStatus.ReadyForPickup,
            OrderStatus.PickedUp
        };

        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .Where(o => o.RestaurantId == restaurantId && activeStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetActiveOrdersByRiderAsync(
        Guid riderId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            OrderStatus.ReadyForPickup,
            OrderStatus.PickedUp
        };

        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryInfo)
            .Where(o => o.DeliveryInfo.RiderId == riderId && activeStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .CountAsync(o => o.CustomerId == customerId, cancellationToken);
    }

    public async Task<int> GetCountByRestaurantIdAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .CountAsync(o => o.RestaurantId == restaurantId, cancellationToken);
    }

    public async Task<int> GetActiveOrdersCountAsync(CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            OrderStatus.Paid,
            OrderStatus.Confirmed,
            OrderStatus.Preparing,
            OrderStatus.ReadyForPickup,
            OrderStatus.PickedUp
        };

        return await _context.Orders
            .CountAsync(o => activeStatuses.Contains(o.Status), cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.AnyAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.AnyAsync(o => o.OrderNumber.Value == orderNumber, cancellationToken);
    }

    public async Task<List<Order>> GetOrdersByStatusOlderThanAsync(
    OrderStatus status,
    DateTime olderThan,
    CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.Status == status && o.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);
    }
}