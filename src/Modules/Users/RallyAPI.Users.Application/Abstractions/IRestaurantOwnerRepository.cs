using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRestaurantOwnerRepository
{
    Task<RestaurantOwner?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RestaurantOwner?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task AddAsync(RestaurantOwner owner, CancellationToken ct = default);
    void Update(RestaurantOwner owner);
}
