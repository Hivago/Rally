using RallyAPI.Marketing.Domain.Entities;

namespace RallyAPI.Marketing.Application.Abstractions;

public interface ICustomerWaitlistRepository
{
    Task AddAsync(CustomerWaitlistEntry entry, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerWaitlistEntry> Items, int Total)> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerWaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default);
}
