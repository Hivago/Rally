using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Queries.GetStaleRestaurantPayouts;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Restaurant payouts stuck Processing (exported, not yet reconciled) longer than the given
/// threshold — the report that catches a bank outage, a missed reconcile upload, or a batch
/// nobody followed up on before it "rots silently" (specs/icici-manual-payout-export.md).
/// </summary>
public class GetStaleRestaurantPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/restaurant/stale", HandleAsync)
            .WithName("GetStaleRestaurantPayouts")
            .WithTags("Admins")
            .WithSummary("List restaurant payouts stuck Processing beyond a day threshold (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken ct,
        [FromQuery] int olderThanDays = 3)
    {
        var result = await sender.Send(new GetStaleRestaurantPayoutsQuery(olderThanDays), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
