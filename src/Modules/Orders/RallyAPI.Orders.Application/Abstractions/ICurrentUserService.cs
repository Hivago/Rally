namespace RallyAPI.Orders.Application.Abstractions;

/// <summary>
/// Provides access to current authenticated user information.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? Phone { get; }
    string? UserType { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }

    bool IsInRole(string role);
    bool IsCustomer { get; }
    bool IsRestaurant { get; }
    bool IsRider { get; }
    bool IsAdmin { get; }
}
