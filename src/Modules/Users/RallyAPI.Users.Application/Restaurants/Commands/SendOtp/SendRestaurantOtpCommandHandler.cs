using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Restaurants.Commands.SendOtp;

internal sealed class SendRestaurantOtpCommandHandler : IRequestHandler<SendRestaurantOtpCommand, Result>
{
    private readonly IOtpService _otpService;
    private readonly IRestaurantRepository _restaurantRepository;

    public SendRestaurantOtpCommandHandler(IOtpService otpService, IRestaurantRepository restaurantRepository)
    {
        _otpService = otpService;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<Result> Handle(SendRestaurantOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure(phoneResult.Error);

        // Unlike Customer OTP, restaurant accounts are admin-provisioned — never auto-create,
        // and never send an OTP to a phone that can't resolve to exactly one account.
        var matches = await _restaurantRepository.GetByPhoneAsync(phoneResult.Value, cancellationToken);
        if (matches.Count == 0)
            return Result.Failure(Error.Validation("No restaurant account found for this phone number."));

        if (matches.Count > 1)
            return Result.Failure(Error.Validation(
                "Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."));

        if (!matches[0].IsActive)
            return Result.Failure(Error.Validation("Restaurant account is inactive. Contact support."));

        var otpResult = await _otpService.GenerateAndSendOtpAsync(phoneResult.Value.Value, cancellationToken);
        if (otpResult.IsFailure)
            return Result.Failure(otpResult.Error);

        return Result.Success();
    }
}
