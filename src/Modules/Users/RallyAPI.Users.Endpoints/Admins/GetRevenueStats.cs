using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRevenueStats;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRevenueStats : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/stats/revenue", HandleAsync)
            .WithName("GetAdminRevenueStats")
            .WithTags("Admins")
            .WithSummary("Revenue and commission breakdown by period (admin)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRevenueStatsQuery(adminId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
