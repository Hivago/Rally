using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Auth.Commands.RefreshToken;
using RallyAPI.Users.Application.Auth.Commands.RevokeToken;

namespace RallyAPI.Users.Endpoints.Auth;

public class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .AllowAnonymous()
    .RequireRateLimiting("refresh");
    }

    public record RefreshRequest(string RefreshToken);

    private static async Task<IResult> HandleAsync(
        RefreshRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? Results.Unauthorized()
            : Results.Ok(result.Value);
    }
}

public class RevokeTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/revoke", HandleAsync)
            .WithName("RevokeToken")
            .WithTags("Auth")
            .RequireAuthorization();
    }

    public record RevokeRequest(string RefreshToken);

    private static async Task<IResult> HandleAsync(
        RevokeRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RevokeTokenCommand(request.RefreshToken);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Token revoked." });
    }
}