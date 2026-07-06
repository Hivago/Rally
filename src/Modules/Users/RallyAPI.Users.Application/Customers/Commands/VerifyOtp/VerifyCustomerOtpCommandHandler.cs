//using MediatR;
//using Microsoft.EntityFrameworkCore.Metadata.Internal;
//using RallyAPI.SharedKernel.Results;
//using RallyAPI.Users.Application.Abstractions;
//using RallyAPI.Users.Domain.Entities;
//using RallyAPI.Users.Domain.ValueObjects;
//using System.Collections.Generic;
//using System.Numerics;
//using System.Security.Principal;
//using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
//using static System.Net.WebRequestMethods;

//namespace RallyAPI.Users.Application.Customers.Commands.VerifyOtp;

//public sealed class VerifyCustomerOtpCommandHandler
//    : IRequestHandler<VerifyCustomerOtpCommand, Result<VerifyCustomerOtpResponse>>
//{
//    private readonly IOtpService _otpService;
//    private readonly ICustomerRepository _customerRepository;
//    private readonly IJwtProvider _jwtProvider;
//    private readonly IUnitOfWork _unitOfWork;

//    public VerifyCustomerOtpCommandHandler(
//        IOtpService otpService,
//        ICustomerRepository customerRepository,
//        IJwtProvider jwtProvider,
//        IUnitOfWork unitOfWork)
//    {
//        _otpService = otpService;
//        _customerRepository = customerRepository;
//        _jwtProvider = jwtProvider;
//        _unitOfWork = unitOfWork;
//    }

//    public async Task<Result<VerifyCustomerOtpResponse>> Handle(
//        VerifyCustomerOtpCommand request,
//        CancellationToken cancellationToken)
//    {
//        // 1. Verify OTP
//        var isValid = await _otpService.VerifyOtpAsync(
//            request.PhoneNumber, request.Otp, cancellationToken);

//        if (!isValid)
//            return Result.Failure<VerifyCustomerOtpResponse>(
//                Error.Validation("Invalid or expired OTP."));

//        // 2. Create phone value object
//        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
//        if (phoneResult.IsFailure)
//            return Result.Failure<VerifyCustomerOtpResponse>(phoneResult.Error);

//        // 3. Find existing customer or create new one
//        var isNewCustomer = false;
//        var customer = await _customerRepository.GetByPhoneAsync(
//            phoneResult.Value, cancellationToken);

//        if (customer is null)
//        {
//            // First time user � auto-register
//            var createResult = Customer.Create(phoneResult.Value);
//            if (createResult.IsFailure)
//                return Result.Failure<VerifyCustomerOtpResponse>(createResult.Error);

//            customer = createResult.Value;
//            await _customerRepository.AddAsync(customer);
//            isNewCustomer = true;
//        }

//        // 4. Check if active
//        if (!customer.IsActive)
//            return Result.Failure<VerifyCustomerOtpResponse>(
//                Error.Validation("Account is deactivated. Contact support."));

//        // 5. Save (for new customers)
//        if (isNewCustomer)
//            await _unitOfWork.SaveChangesAsync(cancellationToken);

//        // 6. Generate real JWT
//        var token = _jwtProvider.GenerateCustomerToken(customer);

//        return new VerifyCustomerOtpResponse(
//            customer.Id,
//            token,
//            isNewCustomer);
//    }
//}
////```

////---

////## What This Does

////1. ** Verifies OTP** via Redis(no more dummy token)
////2. ** Auto-registers** new customers on first login(common pattern for food delivery apps � user enters phone, gets OTP, account created automatically)
////3. ** Returns `isNewCustomer`** so your frontend knows to show a "complete your profile" screen
////4. ** Generates real JWT** with proper claims

////---

////One question before you build � does your `ICustomerRepository` have a `GetByPhoneAsync` method? And does it have an `Add` method?

////Paste a quick look at:
////```
////src/Modules/Users/RallyAPI.Users.Application/Abstractions/ICustomerRepository.cs


using System.Security.Cryptography;
using System.Text;
using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Customers.Commands.VerifyOtp;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Customers.Commands.VerifyOtp;

public sealed class VerifyCustomerOtpCommandHandler
    : IRequestHandler<VerifyCustomerOtpCommand, Result<VerifyCustomerOtpResponse>>
{
    private readonly IOtpService _otpService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyCustomerOtpCommandHandler(
        IOtpService otpService,
        ICustomerRepository customerRepository,
        IJwtProvider jwtProvider,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _otpService = otpService;
        _customerRepository = customerRepository;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyCustomerOtpResponse>> Handle(
        VerifyCustomerOtpCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify OTP
        var isValid = await _otpService.VerifyOtpAsync(
            request.PhoneNumber, request.Otp, cancellationToken);

        if (!isValid)
            return Result.Failure<VerifyCustomerOtpResponse>(
                Error.Validation("Invalid or expired OTP."));

        // 2. Create phone value object
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<VerifyCustomerOtpResponse>(phoneResult.Error);

        // 3. Find or create customer
        var isNewCustomer = false;
        var customer = await _customerRepository.GetByPhoneAsync(
            phoneResult.Value, cancellationToken);

        if (customer is null)
        {
            var createResult = Customer.Create(phoneResult.Value);
            if (createResult.IsFailure)
                return Result.Failure<VerifyCustomerOtpResponse>(createResult.Error);

            customer = createResult.Value;
            await _customerRepository.AddAsync(customer, cancellationToken);
            isNewCustomer = true;
        }

        // 4. Check if active
        if (!customer.IsActive)
            return Result.Failure<VerifyCustomerOtpResponse>(
                Error.Validation("Account is deactivated. Contact support."));

        // 5. Generate token pair
        var tokenPair = _jwtProvider.GenerateCustomerTokenPair(customer);

        // 6. Store refresh token
        var refreshTokenHash = HashToken(tokenPair.RefreshToken);
        var refreshToken = RefreshToken.Create(
            refreshTokenHash, customer.Id, "customer",
            RefreshToken.DefaultLifetime);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyCustomerOtpResponse(
            customer.Id,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresAt,
            isNewCustomer);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}