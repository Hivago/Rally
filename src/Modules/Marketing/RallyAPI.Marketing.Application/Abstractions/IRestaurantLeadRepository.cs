using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Application.Abstractions;

public interface IRestaurantLeadRepository
{
    Task AddAsync(RestaurantLead lead, CancellationToken cancellationToken = default);

    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RestaurantLead> Items, int Total)> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestaurantLead>> GetAllAsync(CancellationToken cancellationToken = default);
}
