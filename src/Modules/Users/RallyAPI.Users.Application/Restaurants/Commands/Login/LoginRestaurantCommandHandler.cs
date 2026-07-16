//using MediatR;
//using RallyAPI.SharedKernel.Results;
//using RallyAPI.Users.Application.Abstractions;
//using RallyAPI.Users.Domain.ValueObjects;

//namespace RallyAPI.Users.Application.Restaurants.Commands.Login;

//internal sealed class LoginRestaurantCommandHandler
//    : IRequestHandler<LoginRestaurantCommand, Result<LoginRestaurantResponse>>
//{
//    private readonly IRestaurantRepository _restaurantRepository;
//    private readonly IPasswordHasher _passwordHasher;
//    private readonly IJwtProvider _jwtProvider;

//    public LoginRestaurantCommandHandler(
//        IRestaurantRepository restaurantRepository,
//        IPasswordHasher passwordHasher,
//        IJwtProvider jwtProvider)
//    {
//        _restaurantRepository = restaurantRepository;
//        _passwordHasher = passwordHasher;
//        _jwtProvider = jwtProvider;
//    }

//    public async Task<Result<LoginRestaurantResponse>> Handle(
//        LoginRestaurantCommand request,
//        CancellationToken cancellationToken)
//    {
//        // Validate email format
//        var emailResult = Email.Create(request.Email);
//        if (emailResult.IsFailure)
//            return Result.Failure<LoginRestaurantResponse>(emailResult.Error);

//        // Find restaurant
//        var restaurant = await _restaurantRepository.GetByEmailAsync(
//            emailResult.Value,
//            cancellationToken);

//        if (restaurant is null)
//            return Result.Failure<LoginRestaurantResponse>(
//                Error.Validation("Invalid email or password."));

//        // Verify password
//        var isValidPassword = _passwordHasher.Verify(request.Password, restaurant.PasswordHash);
//        if (!isValidPassword)
//            return Result.Failure<LoginRestaurantResponse>(
//                Error.Validation("Invalid email or password."));

//        // Check if active
//        if (!restaurant.IsActive)
//            return Result.Failure<LoginRestaurantResponse>(
//                Error.Validation("Restaurant account is inactive. Contact support."));

//        // Generate token
//        var token = _jwtProvider.GenerateRestaurantToken(restaurant);

//        return new LoginRestaurantResponse(restaurant.Id, restaurant.Name, token);
//    }
//}

using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.Login;

internal sealed class LoginRestaurantCommandHandler
    : IRequestHandler<LoginRestaurantCommand, Result<LoginRestaurantResponse>>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LoginRestaurantCommandHandler(
        IRestaurantRepository restaurantRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginRestaurantResponse>> Handle(
        LoginRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<LoginRestaurantResponse>(emailResult.Error);

        var restaurant = await _restaurantRepository.GetByEmailAsync(
            emailResult.Value, cancellationToken);

        if (restaurant is null)
            return Result.Failure<LoginRestaurantResponse>(
                Error.Validation("Invalid email or password."));

        var isValidPassword = _passwordHasher.Verify(request.Password, restaurant.PasswordHash);
        if (!isValidPassword)
            return Result.Failure<LoginRestaurantResponse>(
                Error.Validation("Invalid email or password."));

        if (!restaurant.IsActive)
            return Result.Failure<LoginRestaurantResponse>(
                Error.Validation("Restaurant account is inactive. Contact support."));

        var tokenPair = _jwtProvider.GenerateRestaurantTokenPair(restaurant);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, restaurant.Id, "restaurant",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginRestaurantResponse(
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