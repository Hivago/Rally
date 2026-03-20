using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.VerifyOtp;

namespace RallyAPI.Users.Endpoints.Riders;

public class VerifyOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/riders/otp/verify", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Verify OTP and get token")
            .AllowAnonymous()
.RequireRateLimiting("otp");
    }

    private static async Task<IResult> HandleAsync(
        VerifyRiderOtpRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var command = new VerifyRiderOtpCommand(request.PhoneNumber, request.Otp);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}

public record VerifyRiderOtpRequest(string PhoneNumber, string Otp);