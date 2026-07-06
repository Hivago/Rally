using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    /// <summary>
    /// Grace window during which reuse of a just-rotated refresh token is treated as a
    /// concurrent-refresh race (parallel requests / multiple tabs) rather than theft.
    /// Without this, two tabs refreshing at the same moment would trip theft detection
    /// and force the user to log in again.
    /// </summary>
    private static readonly TimeSpan ConcurrentRefreshGrace = TimeSpan.FromSeconds(30);

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IRiderRepository _riderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ICustomerRepository customerRepository,
        IRiderRepository riderRepository,
        IRestaurantRepository restaurantRepository,
        IAdminRepository adminRepository,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _customerRepository = customerRepository;
        _riderRepository = riderRepository;
        _restaurantRepository = restaurantRepository;
        _adminRepository = adminRepository;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the refresh token
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash, cancellationToken);

        if (storedToken is null)
            return Result.Failure<RefreshTokenResponse>(
                Error.Validation("Invalid refresh token."));

        // 2. Check if token was already used (theft detection!)
        if (storedToken.IsRevoked)
        {
            // A token that was ROTATED (has a successor) and reused within a short grace
            // window is almost always a race between parallel requests or browser tabs —
            // not theft. Let it through and mint a fresh pair instead of revoking the family.
            var isConcurrentRefresh =
                storedToken.ReplacedByTokenId is not null &&
                storedToken.RevokedAt is not null &&
                DateTime.UtcNow - storedToken.RevokedAt.Value <= ConcurrentRefreshGrace;

            if (!isConcurrentRefresh)
            {
                // Genuine reuse of a long-dead or explicitly-revoked token — revoke ALL
                // tokens for this user.
                var allTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(
                    storedToken.UserId, cancellationToken);

                foreach (var t in allTokens)
                    t.Revoke();

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Failure<RefreshTokenResponse>(
                    Error.Validation("Token reuse detected. All sessions revoked. Please login again."));
            }
        }

        // 3. Check if expired
        if (storedToken.IsExpired)
            return Result.Failure<RefreshTokenResponse>(
                Error.Validation("Refresh token expired. Please login again."));

        // 4. Generate new token pair based on user type
        var newTokenPair = await GenerateNewTokenPair(
            storedToken.UserId, storedToken.UserType, cancellationToken);

        if (newTokenPair is null)
            return Result.Failure<RefreshTokenResponse>(
                Error.Validation("User not found."));

        // 5. Rotate: revoke old, create new refresh token
        var newRefreshTokenHash = HashToken(newTokenPair.RefreshToken);
        var newRefreshToken = Domain.Entities.RefreshToken.Create(
            newRefreshTokenHash,
            storedToken.UserId,
            storedToken.UserType,
            storedToken.UserType == "admin"
                ? Domain.Entities.RefreshToken.AdminLifetime
                : Domain.Entities.RefreshToken.DefaultLifetime);

        // Link old → new. Skip if already revoked (concurrent-refresh grace path) so we
        // don't disturb the original rotation chain / move the grace window forward.
        if (!storedToken.IsRevoked)
            storedToken.Revoke(newRefreshToken.Id);
        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResponse(
            newTokenPair.AccessToken,
            newTokenPair.RefreshToken,
            newTokenPair.AccessTokenExpiresAt);
    }

    private async Task<TokenPair?> GenerateNewTokenPair(
        Guid userId, string userType, CancellationToken cancellationToken)
    {
        return userType switch
        {
            "customer" => await GenerateCustomerPair(userId, cancellationToken),
            "rider" => await GenerateRiderPair(userId, cancellationToken),
            "restaurant" => await GenerateRestaurantPair(userId, cancellationToken),
            "admin" => await GenerateAdminPair(userId, cancellationToken),
            _ => null
        };
    }

    private async Task<TokenPair?> GenerateCustomerPair(
        Guid userId, CancellationToken ct)
    {
        var customer = await _customerRepository.GetByIdAsync(userId, ct);
        return customer is null ? null : _jwtProvider.GenerateCustomerTokenPair(customer);
    }

    private async Task<TokenPair?> GenerateRiderPair(
        Guid userId, CancellationToken ct)
    {
        var rider = await _riderRepository.GetByIdAsync(userId, ct);
        return rider is null ? null : _jwtProvider.GenerateRiderTokenPair(rider);
    }

    private async Task<TokenPair?> GenerateRestaurantPair(
        Guid userId, CancellationToken ct)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(userId, ct);
        return restaurant is null ? null : _jwtProvider.GenerateRestaurantTokenPair(restaurant);
    }

    private async Task<TokenPair?> GenerateAdminPair(
        Guid userId, CancellationToken ct)
    {
        var admin = await _adminRepository.GetByIdAsync(userId, ct);
        return admin is null ? null : _jwtProvider.GenerateAdminTokenPair(admin);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}