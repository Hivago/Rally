using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.SendOtp;

namespace RallyAPI.Users.Endpoints.Riders;

public class SendOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/riders/otp/send", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Send OTP to rider phone")
            .AllowAnonymous()
.RequireRateLimiting("otp");

    }

    private static async Task<IResult> HandleAsync(
        SendRiderOtpRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var command = new SendRiderOtpCommand(request.PhoneNumber);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "OTP sent successfully" })
            : result.Error.ToErrorResult();
    }
}

public record SendRiderOtpRequest(string PhoneNumber);