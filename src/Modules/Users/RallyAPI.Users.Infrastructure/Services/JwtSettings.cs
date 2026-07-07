namespace RallyAPI.Users.Infrastructure.Services;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 60;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string PublicKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Raw PEM content of the private key.
    /// When set (e.g. via Railway env var JwtSettings__PrivateKeyPem), takes priority over PrivateKeyPath.
    /// </summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Raw PEM content of the public key.
    /// When set (e.g. via Railway env var JwtSettings__PublicKeyPem), takes priority over PublicKeyPath.
    /// </summary>
    public string PublicKeyPem { get; set; } = string.Empty;
}