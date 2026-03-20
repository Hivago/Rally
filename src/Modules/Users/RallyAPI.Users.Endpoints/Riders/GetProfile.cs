using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Riders.Queries.GetProfile;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Riders;

public class GetProfile : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/riders/profile", HandleAsync)
            .WithTags("Riders")
            .WithSummary("Get rider profile")
            .RequireAuthorization("Rider");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var riderId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = new GetRiderProfileQuery(riderId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}