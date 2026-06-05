using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Riders.Commands.SendOtp;

internal sealed class SendRiderOtpCommandHandler : IRequestHandler<SendRiderOtpCommand, Result>
{
    private readonly IOtpService _otpService;
    private readonly IRiderRepository _riderRepository;

    public SendRiderOtpCommandHandler(IOtpService otpService, IRiderRepository riderRepository)
    {
        _otpService = otpService;
        _riderRepository = riderRepository;
    }

    public async Task<Result> Handle(SendRiderOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure(phoneResult.Error);

        // Rider must be registered first (unlike customer who can self-register)
        var exists = await _riderRepository.ExistsByPhoneAsync(phoneResult.Value, cancellationToken);
        if (!exists)
            return Result.Failure(Error.Validation("Rider not registered. Contact admin."));

        // Generate and send OTP — fails with 429 if rate-limited or locked out
        var otpResult = await _otpService.GenerateAndSendOtpAsync(phoneResult.Value.Value, cancellationToken);
        if (otpResult.IsFailure)
            return Result.Failure(otpResult.Error);

        return Result.Success();
    }
}