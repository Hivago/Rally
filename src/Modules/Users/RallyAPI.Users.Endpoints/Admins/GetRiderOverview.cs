using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRiderOverview;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRiderOverview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/riders/{riderId:guid}", HandleAsync)
            .WithName("GetRiderOverview")
            .WithTags("Admins")
            .WithSummary("Get full rider profile and status for admin panel")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid riderId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRiderOverviewQuery(adminId, riderId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
