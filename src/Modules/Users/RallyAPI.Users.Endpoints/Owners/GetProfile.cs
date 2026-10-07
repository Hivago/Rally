using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Queries.GetProfile;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class GetProfile : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/owners/me", HandleAsync)
            .WithName("GetOwnerProfile")
            .WithTags("Owners")
            .WithSummary("Get the authenticated owner's profile")
            .RequireAuthorization("Owner");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var ownerId = user.GetOwnerId();
        var result = await sender.Send(new GetOwnerProfileQuery(ownerId), ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
