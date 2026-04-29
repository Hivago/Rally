using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderRepository
{
    Task<Rider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Rider?> GetByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
    Task<List<Rider>> GetOnlineRidersAsync(CancellationToken cancellationToken = default);
    Task<Rider?> GetByIdWithKycAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(bool? isOnline = null, CancellationToken cancellationToken = default);
    Task<int> CountPendingKycAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Rider rider, CancellationToken cancellationToken = default);
    void Update(Rider rider, CancellationToken cancellationToken = default);
}