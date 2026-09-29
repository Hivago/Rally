using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RallyAPI.Users.Infrastructure.Services;

public class JwtProvider : IJwtProvider
{
    private readonly JwtSettings _settings;
    private readonly RSA _privateKey;

    public JwtProvider(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;

        // Load private key once at startup — prefer PEM string (Railway env var) over file path
        _privateKey = RSA.Create();
        var keyText = !string.IsNullOrWhiteSpace(_settings.PrivateKeyPem)
            ? _settings.PrivateKeyPem.Replace("\\n", "\n")
            : File.ReadAllText(_settings.PrivateKeyPath);
        _privateKey.ImportFromPem(keyText);
    }

    public string GenerateCustomerToken(Customer customer)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("phone", customer.Phone.Value),
            new("role", "Customer"),
            new("user_type", "customer")
        };

        if (!string.IsNullOrEmpty(customer.Name))
            claims.Add(new Claim("name", customer.Name));

        return GenerateToken(claims);
    }

    public string GenerateRiderToken(Rider rider)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, rider.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("phone", rider.Phone.Value),
            new("name", rider.Name),
            new("role", "Rider"),
            new("user_type", "rider"),
            new("kyc_status", rider.KycStatus.ToString())
        };

        return GenerateToken(claims);
    }

    public string GenerateRestaurantToken(Restaurant restaurant)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, restaurant.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, restaurant.Email.Value),
            new("name", restaurant.Name),
            new("role", "Restaurant"),
            new("user_type", "restaurant")
        };

        return GenerateToken(claims);
    }

    public string GenerateAdminToken(Admin admin)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email.Value),
            new("name", admin.Name),
            new("role", admin.Role.ToString()),
            new("user_type", "admin")
        };

        return GenerateToken(claims);
    }

    private string GenerateToken(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Token pair methods ──

    public TokenPair GenerateCustomerTokenPair(Customer customer)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("phone", customer.Phone.Value),
            new("role", "Customer"),
            new("user_type", "customer")
        };

        if (!string.IsNullOrEmpty(customer.Name))
            claims.Add(new Claim("name", customer.Name));

        return GenerateTokenPair(claims);
    }

    public TokenPair GenerateRiderTokenPair(Rider rider)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, rider.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("phone", rider.Phone.Value),
            new("name", rider.Name),
            new("role", "Rider"),
            new("user_type", "rider"),
            new("kyc_status", rider.KycStatus.ToString())
        };

        return GenerateTokenPair(claims);
    }

    public TokenPair GenerateRestaurantTokenPair(Restaurant restaurant, IReadOnlyList<Guid> restaurantIds)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, restaurant.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, restaurant.Email.Value),
            new("name", restaurant.Name),
            new("role", "Restaurant"),
            new("user_type", "restaurant"),
            new("restaurant_ids", string.Join(",", restaurantIds))
        };

        return GenerateTokenPair(claims);
    }

    public TokenPair GenerateAdminTokenPair(Admin admin)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email.Value),
            new("name", admin.Name),
            new("role", admin.Role.ToString()),
            new("user_type", "admin")
        };

        return GenerateTokenPair(claims);
    }

    public TokenPair GenerateOwnerTokenPair(RestaurantOwner owner)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, owner.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, owner.Email.Value),
            new("name", owner.Name),
            new("role", "Owner"),
            new("user_type", "owner")
        };

        return GenerateTokenPair(claims);
    }

    // ── Private helpers ──

    private TokenPair GenerateTokenPair(List<Claim> claims)
    {
        var accessToken = GenerateAccessToken(claims);
        var refreshToken = GenerateRefreshToken();

        var accessExpiry = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var refreshExpiry = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);

        return new TokenPair(accessToken, refreshToken, accessExpiry, refreshExpiry);
    }

    private string GenerateAccessToken(List<Claim> claims)
    {
        var rsaKey = new RsaSecurityKey(_privateKey);
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}