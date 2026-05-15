using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.QuickPauseOutlet;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class QuickPauseOutlet : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owners/me/outlets/{restaurantId:guid}/time-off/quick-pause", HandleAsync)
            .WithName("QuickPauseOutlet")
            .WithTags("Owners")
            .WithSummary("Owner pauses an outlet for N minutes starting now (1–1440 min)")
            .RequireAuthorization("Owner");
    }

    public record QuickPauseRequest(int DurationMinutes, string? Reason);

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        QuickPauseRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new QuickPauseOutletCommand(ownerId, restaurantId, request.DurationMinutes, request.Reason);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Created($"/api/owners/me/outlets/{restaurantId}/time-off/{result.Value.Id}", result.Value);
    }
}
