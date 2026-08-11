using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.VerifyOtp;

public sealed record VerifyOwnerOtpCommand(string PhoneNumber, string Otp)
    : IRequest<Result<VerifyOwnerOtpResponse>>;

public sealed record VerifyOwnerOtpResponse(
    Guid OwnerId,
    string Name,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt);
