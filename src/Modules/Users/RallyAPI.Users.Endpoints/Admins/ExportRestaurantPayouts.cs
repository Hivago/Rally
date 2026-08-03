using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.GenerateRestaurantPayoutExport;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Generates the weekly ICICI bulk-transfer file for restaurant payouts. Only Pending
/// payouts for the exact period are eligible; each included payout flips to Processing
/// so it can never be re-exported. The file streams as the response body; export metadata
/// (batch id, row count, control-sum, excluded owners) rides along in the
/// X-Payout-Export-Meta header since a file response can't also carry a JSON body.
/// </summary>
public class ExportRestaurantPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/restaurant/export", HandleAsync)
            .WithName("ExportRestaurantPayouts")
            .WithTags("Admins")
            .WithSummary("Generate the weekly ICICI bulk-transfer file for restaurant payouts (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        HttpContext httpContext,
        CancellationToken ct,
        [FromQuery] DateOnly periodStart,
        [FromQuery] DateOnly periodEnd)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var result = await sender.Send(
            new GenerateRestaurantPayoutExportCommand(periodStart, periodEnd, adminId), ct);

        if (result.IsFailure)
            return result.Error.ToErrorResult();

        var meta = JsonSerializer.Serialize(new
        {
            exportBatchId = result.Value.ExportBatchId,
            rowCount = result.Value.RowCount,
            controlSumTotal = result.Value.ControlSumTotal,
            excluded = result.Value.Excluded,
            generatedAtUtc = result.Value.GeneratedAtUtc
        });
        httpContext.Response.Headers["X-Payout-Export-Meta"] = meta;
        httpContext.Response.Headers["Access-Control-Expose-Headers"] = "X-Payout-Export-Meta";

        return Results.File(
            result.Value.FileContent,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: result.Value.FileName);
    }
}
