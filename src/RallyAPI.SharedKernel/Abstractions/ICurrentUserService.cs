namespace RallyAPI.SharedKernel.Abstractions;

/// <summary>
/// Provides access to current authenticated user information.
/// Lives in SharedKernel so any module can reference it without cross-module coupling.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? Phone { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// For a restaurant-role caller: the full set of outlet ids (same owner, active) this
    /// login is authorized to act on, including <see cref="UserId"/> itself. Falls back to
    /// <c>[UserId]</c> when the token predates the <c>restaurant_ids</c> claim or has no
    /// authenticated user.
    /// </summary>
    IReadOnlyList<Guid> RestaurantIds { get; }

    bool IsInRole(string role);
    bool IsCustomer { get; }
    bool IsRestaurant { get; }
    bool IsRider { get; }
    bool IsAdmin { get; }
}
