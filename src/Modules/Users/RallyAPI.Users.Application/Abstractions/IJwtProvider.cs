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
    TokenPair GenerateAdminTokenPair(Admin admin);
    TokenPair GenerateOwnerTokenPair(RestaurantOwner owner);
}

// The response both tokens travel in
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);