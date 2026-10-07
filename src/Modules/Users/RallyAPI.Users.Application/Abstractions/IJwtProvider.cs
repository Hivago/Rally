using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Abstractions;

public interface IJwtProvider
{
    string GenerateCustomerToken(Customer customer);
    string GenerateRiderToken(Rider rider);
    string GenerateRestaurantToken(Restaurant restaurant);
    string GenerateAdminToken(Admin admin);

    //Refresh token support
    TokenPair GenerateCustomerTokenPair(Customer customer);
    TokenPair GenerateRiderTokenPair(Rider rider);
    TokenPair GenerateRestaurantTokenPair(Restaurant restaurant, IReadOnlyList<Guid> restaurantIds);

    /// <summary>
    /// Outlet-scoped token for an OWNER who switched into an outlet. Same as the restaurant
    /// token plus owner_id + owner_access=true, which unlocks the owner-level endpoints.
    /// Only the owner switch flow (and its refresh) may issue this; direct restaurant logins never do.
    /// </summary>
    TokenPair GenerateOwnerOutletTokenPair(Restaurant restaurant, Guid ownerId, IReadOnlyList<Guid> restaurantIds);
    TokenPair GenerateAdminTokenPair(Admin admin);
    TokenPair GenerateOwnerTokenPair(RestaurantOwner owner);
}

// The response both tokens travel in
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);