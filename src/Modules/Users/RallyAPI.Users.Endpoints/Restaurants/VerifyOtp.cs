using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.VerifyOtp;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class VerifyOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurants/otp/verify", HandleAsync)
            .WithName("RestaurantVerifyOtp")
            .WithTags("Restaurants")
            .WithSummary("Verify a restaurant login OTP and issue tokens")
            .AllowAnonymous()
            .RequireRateLimiting("otp");
    }

    public record VerifyRestaurantOtpRequest(string PhoneNumber, string Otp);

    private static async Task<IResult> HandleAsync(
        VerifyRestaurantOtpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new VerifyRestaurantOtpCommand(request.PhoneNumber, request.Otp);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
