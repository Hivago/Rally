using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutSummary;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRestaurantPayoutSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/restaurant/summary", HandleAsync)
            .WithName("GetRestaurantPayoutSummary")
            .WithTags("Admins")
            .WithSummary("Stats bar for the restaurant payouts page (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetRestaurantPayoutSummaryQuery(adminId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
