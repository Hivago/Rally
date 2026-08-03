using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Owners.Commands.VerifyOtp;

internal sealed class VerifyOwnerOtpCommandHandler
    : IRequestHandler<VerifyOwnerOtpCommand, Result<VerifyOwnerOtpResponse>>
{
    private readonly IOtpService _otpService;
    private readonly IRestaurantOwnerRepository _ownerRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyOwnerOtpCommandHandler(
        IOtpService otpService,
        IRestaurantOwnerRepository ownerRepository,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _otpService = otpService;
        _ownerRepository = ownerRepository;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyOwnerOtpResponse>> Handle(
        VerifyOwnerOtpCommand request,
        CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<VerifyOwnerOtpResponse>(phoneResult.Error);

        var isValid = await _otpService.VerifyOtpAsync(phoneResult.Value.Value, request.Otp, cancellationToken);
        if (!isValid)
            return Result.Failure<VerifyOwnerOtpResponse>(Error.Validation("Invalid or expired OTP."));

        // Re-check existence/uniqueness at verify time too — state may have changed since send.
        // Deactivated duplicates don't count against the real account.
        var matches = (await _ownerRepository.GetByPhoneAsync(phoneResult.Value, cancellationToken))
            .Where(o => o.IsActive)
            .ToList();

        if (matches.Count == 0)
            return Result.Failure<VerifyOwnerOtpResponse>(Error.Validation("No active owner account found for this phone number."));

        if (matches.Count > 1)
            return Result.Failure<VerifyOwnerOtpResponse>(Error.Validation(
                "Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."));

        var owner = matches[0];

        var tokenPair = _jwtProvider.GenerateOwnerTokenPair(owner);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, owner.Id, "owner",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyOwnerOtpResponse(
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
