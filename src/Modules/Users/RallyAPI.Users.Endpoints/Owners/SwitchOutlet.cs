using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.SwitchOutlet;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class SwitchOutlet : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/owners/outlets/{restaurantId:guid}/switch", HandleAsync)
            .WithName("SwitchToOutlet")
            .WithTags("Owners")
            .WithSummary("Exchange owner token for a restaurant-scoped token for the given outlet")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var ownerId = user.GetOwnerId();
        var result = await sender.Send(new SwitchToOutletCommand(ownerId, restaurantId), ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
