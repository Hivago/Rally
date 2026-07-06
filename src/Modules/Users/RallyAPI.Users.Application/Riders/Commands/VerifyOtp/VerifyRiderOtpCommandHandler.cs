//using MediatR;
//using RallyAPI.SharedKernel.Results;
//using RallyAPI.Users.Application.Abstractions;
//using RallyAPI.Users.Domain.ValueObjects;

//namespace RallyAPI.Users.Application.Riders.Commands.VerifyOtp;

//internal sealed class VerifyRiderOtpCommandHandler
//    : IRequestHandler<VerifyRiderOtpCommand, Result<VerifyRiderOtpResponse>>
//{
//    private readonly IOtpService _otpService;
//    private readonly IRiderRepository _riderRepository;
//    private readonly IJwtProvider _jwtProvider;

//    public VerifyRiderOtpCommandHandler(
//        IOtpService otpService,
//        IRiderRepository riderRepository,
//        IJwtProvider jwtProvider)
//    {
//        _otpService = otpService;
//        _riderRepository = riderRepository;
//        _jwtProvider = jwtProvider;
//    }

//    public async Task<Result<VerifyRiderOtpResponse>> Handle(
//        VerifyRiderOtpCommand request,
//        CancellationToken cancellationToken)
//    {
//        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
//        if (phoneResult.IsFailure)
//            return Result.Failure<VerifyRiderOtpResponse>(phoneResult.Error);

//        var isValidOtp = await _otpService.VerifyOtpAsync(
//            phoneResult.Value.Value,
//            request.Otp,
//            cancellationToken);

//        if (!isValidOtp)
//            return Result.Failure<VerifyRiderOtpResponse>(Error.Validation("Invalid OTP."));

//        var rider = await _riderRepository.GetByPhoneAsync(phoneResult.Value, cancellationToken);
//        if (rider is null)
//            return Result.Failure<VerifyRiderOtpResponse>(Error.NotFound("Rider", Guid.Empty));

//        if (!rider.IsActive)
//            return Result.Failure<VerifyRiderOtpResponse>(Error.Validation("Rider account is inactive."));

//        var token = _jwtProvider.GenerateRiderToken(rider);

//        return new VerifyRiderOtpResponse(rider.Id, token, rider.KycStatus.ToString());
//    }
//}


using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Riders.Commands.VerifyOtp;

internal sealed class VerifyRiderOtpCommandHandler
    : IRequestHandler<VerifyRiderOtpCommand, Result<VerifyRiderOtpResponse>>
{
    private readonly IOtpService _otpService;
    private readonly IRiderRepository _riderRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyRiderOtpCommandHandler(
        IOtpService otpService,
        IRiderRepository riderRepository,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _otpService = otpService;
        _riderRepository = riderRepository;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyRiderOtpResponse>> Handle(
        VerifyRiderOtpCommand request,
        CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<VerifyRiderOtpResponse>(phoneResult.Error);

        var isValidOtp = await _otpService.VerifyOtpAsync(
            phoneResult.Value.Value, request.Otp, cancellationToken);

        if (!isValidOtp)
            return Result.Failure<VerifyRiderOtpResponse>(
                Error.Validation("Invalid OTP."));

        var rider = await _riderRepository.GetByPhoneAsync(
            phoneResult.Value, cancellationToken);

        if (rider is null)
            return Result.Failure<VerifyRiderOtpResponse>(
                Error.NotFound("Rider", Guid.Empty));

        if (!rider.IsActive)
            return Result.Failure<VerifyRiderOtpResponse>(
                Error.Validation("Rider account is inactive."));

        var tokenPair = _jwtProvider.GenerateRiderTokenPair(rider);

        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, rider.Id, "rider",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyRiderOtpResponse(
            rider.Id,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresAt,
            rider.KycStatus.ToString());
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}