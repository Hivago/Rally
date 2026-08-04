using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.SendOtp;

namespace RallyAPI.Users.Endpoints.Owners;

public class SendOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owners/otp/send", HandleAsync)
            .WithName("OwnerSendOtp")
            .WithTags("Owners")
            .WithSummary("Send a login OTP to an owner's registered phone number")
            .AllowAnonymous()
            .RequireRateLimiting("otp");
    }

    public record SendOwnerOtpRequest(string PhoneNumber);

    private static async Task<IResult> HandleAsync(
        SendOwnerOtpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SendOwnerOtpCommand(request.PhoneNumber);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "OTP sent successfully" });
    }
}
