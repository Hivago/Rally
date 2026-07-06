using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Owners.Commands.SwitchOutlet;

internal sealed class SwitchToOutletCommandHandler
    : IRequestHandler<SwitchToOutletCommand, Result<SwitchToOutletResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SwitchToOutletCommandHandler(
        IRestaurantRepository restaurantRepository,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SwitchToOutletResponse>> Handle(
        SwitchToOutletCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result.Failure<SwitchToOutletResponse>(Error.NotFound("Restaurant", request.RestaurantId));

        if (restaurant.OwnerId != request.OwnerId)
            return Result.Failure<SwitchToOutletResponse>(
                Error.Forbidden("You do not have access to this outlet."));

        if (!restaurant.IsActive)
            return Result.Failure<SwitchToOutletResponse>(
                Error.Validation("Outlet is inactive."));

        var tokenPair = _jwtProvider.GenerateRestaurantTokenPair(restaurant);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, restaurant.Id, "restaurant",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SwitchToOutletResponse(
            restaurant.Id,
            restaurant.Name,
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
