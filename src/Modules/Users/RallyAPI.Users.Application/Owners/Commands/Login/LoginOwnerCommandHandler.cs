using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Owners.Commands.Login;

internal sealed class LoginOwnerCommandHandler
    : IRequestHandler<LoginOwnerCommand, Result<LoginOwnerResponse>>
{
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LoginOwnerCommandHandler(
        IRestaurantOwnerRepository ownerRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _ownerRepository = ownerRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginOwnerResponse>> Handle(
        LoginOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<LoginOwnerResponse>(emailResult.Error);

        var owner = await _ownerRepository.GetByEmailAsync(emailResult.Value, cancellationToken);

        if (owner is null)
            return Result.Failure<LoginOwnerResponse>(
                Error.Validation("Invalid email or password."));

        var isValidPassword = _passwordHasher.Verify(request.Password, owner.PasswordHash);
        if (!isValidPassword)
            return Result.Failure<LoginOwnerResponse>(
                Error.Validation("Invalid email or password."));

        if (!owner.IsActive)
            return Result.Failure<LoginOwnerResponse>(
                Error.Validation("Owner account is inactive. Contact support."));

        var tokenPair = _jwtProvider.GenerateOwnerTokenPair(owner);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, owner.Id, "owner",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginOwnerResponse(
            owner.Id,
            owner.Name,
            owner.Email.Value,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresAt);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
