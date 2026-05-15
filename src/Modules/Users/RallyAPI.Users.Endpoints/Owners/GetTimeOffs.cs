using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Queries.GetTimeOffs;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class GetTimeOffs : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/owners/me/outlets/{restaurantId:guid}/time-off", HandleAsync)
            .WithName("GetOutletTimeOffs")
            .WithTags("Owners")
            .WithSummary("List scheduled time offs for an outlet (default: upcoming + active only)")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        bool? includeCancelled,
        bool? includePast,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var query = new GetTimeOffsQuery(
            ownerId,
            restaurantId,
            includeCancelled ?? false,
            includePast ?? false);

        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
