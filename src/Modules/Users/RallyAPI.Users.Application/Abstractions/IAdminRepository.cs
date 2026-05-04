using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Abstractions;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Admin?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task AddAsync(Admin admin, CancellationToken cancellationToken = default);
    void Update(Admin admin);

    Task<(IReadOnlyList<Admin> Items, int TotalCount)> GetPagedAsync(
        AdminRole? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}