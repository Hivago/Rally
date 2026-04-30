using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRiderPayoutSummary;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRiderPayoutSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/rider/summary", HandleAsync)
            .WithName("GetRiderPayoutSummary")
            .WithTags("Admins")
            .WithSummary("Stats bar for the rider payouts page (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Guid adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRiderPayoutSummaryQuery(adminId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
