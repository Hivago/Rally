using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.SendOtp;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class SendOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurants/otp/send", HandleAsync)
            .WithName("RestaurantSendOtp")
            .WithTags("Restaurants")
            .WithSummary("Send a login OTP to a restaurant's registered phone number")
            .AllowAnonymous()
            .RequireRateLimiting("otp");
    }

    public record SendRestaurantOtpRequest(string PhoneNumber);

    private static async Task<IResult> HandleAsync(
        SendRestaurantOtpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SendRestaurantOtpCommand(request.PhoneNumber);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "OTP sent successfully" });
    }
}
