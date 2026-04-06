using RallyAPI.Orders.Domain.Entities;
using RallyAPI.Orders.Domain.Enums;

namespace RallyAPI.Orders.Domain.Abstractions;

/// <summary>
/// Repository interface for Order aggregate.
/// Implementations handle persistence details.
/// </summary>
public interface IOrderRepository
{
    // Basic CRUD
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    void Update(Order order);
    void Update(Order order, CancellationToken cancellationToken = default);
    void Remove(Order order);

    // Queries
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        Guid customerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(
        Guid restaurantId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByStatusAsync(
        OrderStatus status,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetActiveOrdersByRestaurantAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetActiveOrdersByRiderAsync(
        Guid riderId,
        CancellationToken cancellationToken = default);

    Task<List<Order>> GetOrdersByStatusOlderThanAsync(
    OrderStatus status,
    DateTime olderThan,
    CancellationToken cancellationToken = default);

    // Escalated orders (admin view)
    Task<IReadOnlyList<Order>> GetEscalatedAsync(
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
    Task<int> CountEscalatedAsync(CancellationToken cancellationToken = default);

    // Counts
    Task<int> GetCountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<int> GetCountByRestaurantIdAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<int> GetActiveOrdersCountAsync(CancellationToken cancellationToken = default);

    // Filtered query (admin)
    Task<IReadOnlyList<Order>> GetFilteredAsync(
        OrderStatus? status = null,
        Guid? restaurantId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetFilteredCountAsync(
        OrderStatus? status = null,
        Guid? restaurantId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    // Existence checks
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
}