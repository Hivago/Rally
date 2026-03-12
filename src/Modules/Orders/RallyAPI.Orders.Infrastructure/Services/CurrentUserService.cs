using Microsoft.AspNetCore.Http;
using RallyAPI.Orders.Application.Abstractions;
using System.Security.Claims;

namespace RallyAPI.Orders.Infrastructure.Services;

/// <summary>
/// Implementation of current user service using HttpContext.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? UserName => User?.FindFirst(ClaimTypes.Name)?.Value
        ?? User?.FindFirst("name")?.Value;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    public string? Phone => User?.FindFirst(ClaimTypes.MobilePhone)?.Value
        ?? User?.FindFirst("phone")?.Value;

    public string? UserType => User?.FindFirst("user_type")?.Value?.ToLowerInvariant();

    public IReadOnlyList<string> Roles
    {
        get
        {
            var standardRoles = User?.FindAll(ClaimTypes.Role)
                .Select(c => c.Value) ?? Enumerable.Empty<string>();

            var customRoles = User?.FindAll("role")
                .Select(c => c.Value) ?? Enumerable.Empty<string>();

            return standardRoles
                .Concat(customRoles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    public bool IsCustomer =>
        string.Equals(UserType, "customer", StringComparison.OrdinalIgnoreCase) ||
        IsInRole("Customer");

    public bool IsRestaurant =>
        string.Equals(UserType, "restaurant", StringComparison.OrdinalIgnoreCase) ||
        IsInRole("Restaurant");

    public bool IsRider =>
        string.Equals(UserType, "rider", StringComparison.OrdinalIgnoreCase) ||
        IsInRole("Rider");

    public bool IsAdmin =>
        string.Equals(UserType, "admin", StringComparison.OrdinalIgnoreCase) ||
        IsInRole("Admin");
}
