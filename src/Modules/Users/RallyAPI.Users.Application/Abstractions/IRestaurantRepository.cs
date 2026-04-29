using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRestaurantRepository
{
    Task<Restaurant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Restaurant?> GetByIdWithScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Restaurant?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Restaurant>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Restaurant> Items, int TotalCount)> GetPagedAsync(
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(Restaurant restaurant, CancellationToken cancellationToken = default);
    void Update(Restaurant restaurant, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}