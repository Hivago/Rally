using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.ScheduleTimeOff;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class ScheduleTimeOff : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owners/me/outlets/{restaurantId:guid}/time-off", HandleAsync)
            .WithName("ScheduleOutletTimeOff")
            .WithTags("Owners")
            .WithSummary("Owner schedules a closure window for an outlet (e.g. Diwali, training)")
            .RequireAuthorization("Owner");
    }

    public record ScheduleTimeOffRequest(DateTime StartsAtUtc, DateTime EndsAtUtc, string? Reason);

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ScheduleTimeOffRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new ScheduleTimeOffCommand(
            ownerId,
            restaurantId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.Reason);

        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Created($"/api/owners/me/outlets/{restaurantId}/time-off/{result.Value.Id}", result.Value);
    }
}
