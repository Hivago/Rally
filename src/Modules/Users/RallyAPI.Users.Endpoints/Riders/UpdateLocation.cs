using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Commands.UpdateLocation;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class UpdateLocation : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/riders/location", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Update rider current location")
            .RequireAuthorization("Rider");
    }

    private static async Task<IResult> HandleAsync(
        UpdateRiderLocationRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new UpdateRiderLocationCommand(
            riderId,
            request.Latitude,
            request.Longitude);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok()
            : result.Error.ToErrorResult();
    }
}

public record UpdateRiderLocationRequest(decimal Latitude, decimal Longitude);