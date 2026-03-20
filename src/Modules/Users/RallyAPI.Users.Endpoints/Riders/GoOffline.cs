using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.GoOffline;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class GoOffline : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/riders/status/offline", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Set rider status to offline")
            .RequireAuthorization("Rider");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new GoOfflineCommand(riderId);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "You are now offline" })
            : result.Error.ToErrorResult();
    }
}