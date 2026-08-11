using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.VerifyOtp;

namespace RallyAPI.Users.Endpoints.Owners;

public class VerifyOtp : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owners/otp/verify", HandleAsync)
            .WithName("OwnerVerifyOtp")
            .WithTags("Owners")
            .WithSummary("Verify an owner login OTP and issue tokens")
            .AllowAnonymous()
            .RequireRateLimiting("otp");
    }

    public record VerifyOwnerOtpRequest(string PhoneNumber, string Otp);

    private static async Task<IResult> HandleAsync(
        VerifyOwnerOtpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new VerifyOwnerOtpCommand(request.PhoneNumber, request.Otp);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
