using RallyAPI.SharedKernel.Domain;

namespace RallyAPI.Users.Domain.Entities;

public sealed class RefreshToken : BaseEntity
{
    public string TokenHash { get; private set; }     // SHA256 hash of the token
    public Guid UserId { get; private set; }           // Who owns this token
    public string UserType { get; private set; }       // "customer", "rider", "restaurant", "admin"
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; } // For rotation chain

    /// <summary>
    /// Default refresh-token lifetime for end users (customer / rider / restaurant / owner).
    /// Sliding window — a fresh token with this lifetime is minted on every rotation, so an
    /// active user effectively never has to log in again within this window.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(60);

    /// <summary>
    /// Shorter refresh-token lifetime for admin sessions (higher-privilege, re-auth sooner).
    /// </summary>
    public static readonly TimeSpan AdminLifetime = TimeSpan.FromDays(1);

    private RefreshToken() { }

    public static RefreshToken Create(
        string tokenHash,
        Guid userId,
        string userType,
        TimeSpan lifetime)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            UserId = userId,
            UserType = userType,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            IsRevoked = false
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(Guid? replacedByTokenId = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}