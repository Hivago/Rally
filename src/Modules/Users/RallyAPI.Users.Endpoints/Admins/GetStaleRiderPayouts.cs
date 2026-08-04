using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetStaleRiderPayouts;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Rider payouts stuck Processing beyond the given threshold. Mirrors GetStaleRestaurantPayouts.
/// </summary>
public class GetStaleRiderPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/rider/stale", HandleAsync)
            .WithName("GetStaleRiderPayouts")
            .WithTags("Admins")
            .WithSummary("List rider payouts stuck Processing beyond a day threshold (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken ct,
        [FromQuery] int olderThanDays = 3)
    {
        var result = await sender.Send(new GetStaleRiderPayoutsQuery(olderThanDays), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
