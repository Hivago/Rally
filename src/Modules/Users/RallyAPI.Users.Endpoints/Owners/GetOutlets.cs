using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Queries.GetOutlets;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class GetOutlets : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/owners/me/outlets", HandleAsync)
            .WithName("GetOwnerOutlets")
            .WithTags("Owners")
            .WithSummary("List all outlets owned by the authenticated owner")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var ownerId = user.GetOwnerId();
        var result = await sender.Send(new GetOwnerOutletsQuery(ownerId), ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
