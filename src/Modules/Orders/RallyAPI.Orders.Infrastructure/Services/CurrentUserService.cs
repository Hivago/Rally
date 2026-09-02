using Microsoft.AspNetCore.Http;
using RallyAPI.SharedKernel.Abstractions;
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
            var userIdClaim = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? UserName => User?.FindFirst("name")?.Value;

    public string? Email => User?.FindFirst("email")?.Value;

    public string? Phone => User?.FindFirst("phone")?.Value;

    public IReadOnlyList<string> Roles
    {
        get
        {
            var roles = User?.FindAll("role")
                .Select(c => c.Value)
                .ToList();
            return roles ?? new List<string>();
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    // Admin JWTs carry the specific AdminRole (Support/CityAdmin/SuperAdmin) in the "role" claim,
    // never the literal string "Admin" — so IsAdmin must check "user_type" like the auth policies do
    // (Program.cs "Admin" policy uses RequireClaim("user_type", "admin")), not IsInRole("Admin").
    public bool IsCustomer => IsInRole("Customer") || IsInRole("customer");
    public bool IsRestaurant => IsInRole("Restaurant") || IsInRole("restaurant");
    public bool IsRider => IsInRole("Rider") || IsInRole("rider");
    public bool IsAdmin => User?.HasClaim("user_type", "admin") ?? false;
}