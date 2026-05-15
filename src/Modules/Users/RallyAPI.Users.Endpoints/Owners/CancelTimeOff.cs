using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.CancelTimeOff;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class CancelTimeOff : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/owners/me/outlets/{restaurantId:guid}/time-off/{timeOffId:guid}", HandleAsync)
            .WithName("CancelOutletTimeOff")
            .WithTags("Owners")
            .WithSummary("Owner cancels a scheduled time off (soft — preserves history)")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        Guid timeOffId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CancelTimeOffCommand(ownerId, restaurantId, timeOffId);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.NoContent();
    }
}
