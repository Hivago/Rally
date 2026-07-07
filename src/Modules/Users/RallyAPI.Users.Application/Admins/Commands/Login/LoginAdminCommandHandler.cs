//using MediatR;
//using RallyAPI.SharedKernel.Results;
//using RallyAPI.Users.Application.Abstractions;
//using RallyAPI.Users.Domain.ValueObjects;

//namespace RallyAPI.Users.Application.Admins.Commands.Login;

//internal sealed class LoginAdminCommandHandler
//    : IRequestHandler<LoginAdminCommand, Result<LoginAdminResponse>>
//{
//    private readonly IAdminRepository _adminRepository;
//    private readonly IPasswordHasher _passwordHasher;
//    private readonly IJwtProvider _jwtProvider;

//    public LoginAdminCommandHandler(
//        IAdminRepository adminRepository,
//        IPasswordHasher passwordHasher,
//        IJwtProvider jwtProvider)
//    {
//        _adminRepository = adminRepository;
//        _passwordHasher = passwordHasher;
//        _jwtProvider = jwtProvider;
//    }

//    public async Task<Result<LoginAdminResponse>> Handle(
//        LoginAdminCommand request,
//        CancellationToken cancellationToken)
//    {
//        var emailResult = Email.Create(request.Email);
//        if (emailResult.IsFailure)
//            return Result.Failure<LoginAdminResponse>(emailResult.Error);

//        var admin = await _adminRepository.GetByEmailAsync(
//            emailResult.Value,
//            cancellationToken);

//        if (admin is null)
//            return Result.Failure<LoginAdminResponse>(
//                Error.Validation("Invalid email or password."));

//        var isValidPassword = _passwordHasher.Verify(request.Password, admin.PasswordHash);
//        if (!isValidPassword)
//            return Result.Failure<LoginAdminResponse>(
//                Error.Validation("Invalid email or password."));

//        if (!admin.IsActive)
//            return Result.Failure<LoginAdminResponse>(
//                Error.Validation("Admin account is inactive."));

//        var token = _jwtProvider.GenerateAdminToken(admin);

//        return new LoginAdminResponse(
//            admin.Id,
//            admin.Name,
//            admin.Role.ToString(),
//            token);
//    }
//}



using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Admins.Commands.Login;

internal sealed class LoginAdminCommandHandler
    : IRequestHandler<LoginAdminCommand, Result<LoginAdminResponse>>
{
    private readonly IAdminRepository _adminRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LoginAdminCommandHandler(
        IAdminRepository adminRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _adminRepository = adminRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LoginAdminResponse>> Handle(
        LoginAdminCommand request,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<LoginAdminResponse>(emailResult.Error);

        var admin = await _adminRepository.GetByEmailAsync(
            emailResult.Value, cancellationToken);

        if (admin is null)
            return Result.Failure<LoginAdminResponse>(
                Error.Validation("Invalid email or password."));

        var isValidPassword = _passwordHasher.Verify(request.Password, admin.PasswordHash);
        if (!isValidPassword)
            return Result.Failure<LoginAdminResponse>(
                Error.Validation("Invalid email or password."));

        if (!admin.IsActive)
            return Result.Failure<LoginAdminResponse>(
                Error.Validation("Admin account is inactive."));

        // Generate token pair
        var tokenPair = _jwtProvider.GenerateAdminTokenPair(admin);

        // Store refresh token
        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, admin.Id, "admin",
            RefreshToken.AdminLifetime); // Shorter for admins

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginAdminResponse(
            admin.Id,
            admin.Name,
            admin.Role.ToString(),
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