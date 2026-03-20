using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace RallyAPI.Integration.Tests.Infrastructure;

/// <summary>
/// Generates RS256 JWTs for test users.
/// Uses the same RSA key as IntegrationTestFactory so tokens pass validation.
/// </summary>
public sealed class TestJwtHelper
{
    public const string Issuer   = "rally-api";
    public const string Audience = "rally-app";

    private readonly RSA _rsa;

    public TestJwtHelper(RSA rsa)
    {
        _rsa = rsa;
    }

    public string CreateCustomerToken(Guid customerId, string name = "Test Customer")
        => CreateToken(customerId, name, "customer");

    public string CreateRestaurantToken(Guid restaurantId, string name = "Test Restaurant")
        => CreateToken(restaurantId, name, "restaurant");

    public string CreateRiderToken(Guid riderId, string name = "Test Rider")
        => CreateToken(riderId, name, "rider");

    public string CreateAdminToken(Guid adminId, string name = "Test Admin")
        => CreateToken(adminId, name, "admin");

    private string CreateToken(Guid userId, string name, string userType)
    {
        var key         = new RsaSecurityKey(_rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            // sub maps to ClaimTypes.NameIdentifier via the JWT Bearer handler
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, name),
            new("user_type", userType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow.AddMinutes(-1),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
