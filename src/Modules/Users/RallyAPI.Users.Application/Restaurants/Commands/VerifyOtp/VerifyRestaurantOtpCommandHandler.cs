using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.VerifyOtp;

internal sealed class VerifyRestaurantOtpCommandHandler
    : IRequestHandler<VerifyRestaurantOtpCommand, Result<VerifyRestaurantOtpResponse>>
{
    private readonly IOtpService _otpService;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyRestaurantOtpCommandHandler(
        IOtpService otpService,
        IRestaurantRepository restaurantRepository,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _otpService = otpService;
        _restaurantRepository = restaurantRepository;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyRestaurantOtpResponse>> Handle(
        VerifyRestaurantOtpCommand request,
        CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<VerifyRestaurantOtpResponse>(phoneResult.Error);

        var isValid = await _otpService.VerifyOtpAsync(phoneResult.Value.Value, request.Otp, cancellationToken);
        if (!isValid)
            return Result.Failure<VerifyRestaurantOtpResponse>(Error.Validation("Invalid or expired OTP."));

        // Re-check existence/uniqueness at verify time too — state may have changed since send.
        // Deactivated duplicates don't count against the real account.
        var matches = (await _restaurantRepository.GetByPhoneAsync(phoneResult.Value, cancellationToken))
            .Where(r => r.IsActive)
            .ToList();

        if (matches.Count == 0)
            return Result.Failure<VerifyRestaurantOtpResponse>(Error.Validation("No active restaurant account found for this phone number."));

        if (matches.Count > 1)
            return Result.Failure<VerifyRestaurantOtpResponse>(Error.Validation(
                "Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."));

        var restaurant = matches[0];

        var tokenPair = _jwtProvider.GenerateRestaurantTokenPair(restaurant);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, restaurant.Id, "restaurant",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyRestaurantOtpResponse(
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
