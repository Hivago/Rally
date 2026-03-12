using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Auth.Commands.RevokeToken;

public sealed class RevokeTokenCommandHandler
    : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RevokeTokenCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
            return Result.Success(); // Don't reveal if token existed

        if (storedToken.UserId != request.RequestingUserId)
            return Result.Success(); // Don't reveal ownership details

        storedToken.Revoke();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
