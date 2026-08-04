using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.ListRiderPayoutExportBatches;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Lists recent rider payout export batches. Mirrors ListRestaurantPayoutExportBatches.
/// </summary>
public class ListRiderPayoutExportBatches : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/rider/batches", HandleAsync)
            .WithName("ListRiderPayoutExportBatches")
            .WithTags("Admins")
            .WithSummary("List recent rider payout export batches (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken ct,
        [FromQuery] PayoutExportBatchStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(
            new ListRiderPayoutExportBatchesQuery(status, page, pageSize), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
