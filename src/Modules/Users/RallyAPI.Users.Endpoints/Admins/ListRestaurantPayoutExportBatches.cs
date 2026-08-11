using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Queries.ListRestaurantPayoutExportBatches;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Lists recent restaurant payout export batches so an admin can recover a batch's id (and its
/// live Processing/Paid/Failed breakdown) to reconcile it — the export response only carries
/// the id once, in a response header.
/// </summary>
public class ListRestaurantPayoutExportBatches : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/restaurant/batches", HandleAsync)
            .WithName("ListRestaurantPayoutExportBatches")
            .WithTags("Admins")
            .WithSummary("List recent restaurant payout export batches (admin panel)")
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
            new ListRestaurantPayoutExportBatchesQuery(status, page, pageSize), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
