using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Restaurants.Commands.VerifyOtp;

public sealed record VerifyRestaurantOtpCommand(string PhoneNumber, string Otp)
    : IRequest<Result<VerifyRestaurantOtpResponse>>;

public sealed record VerifyRestaurantOtpResponse(
    Guid RestaurantId,
    string Name,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt);
