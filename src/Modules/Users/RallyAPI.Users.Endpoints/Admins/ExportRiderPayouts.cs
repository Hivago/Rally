using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.GenerateRiderPayoutExport;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Generates the weekly ICICI bulk-transfer file for rider payouts. Mirrors
/// ExportRestaurantPayouts — only Pending payouts for the exact cycle are eligible, each
/// included payout flips to Processing, and export metadata rides along in the
/// X-Payout-Export-Meta header alongside the file response body.
/// </summary>
public class ExportRiderPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/export", HandleAsync)
            .WithName("ExportRiderPayouts")
            .WithTags("Admins")
            .WithSummary("Generate the weekly ICICI bulk-transfer file for rider payouts (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        HttpContext httpContext,
        CancellationToken ct,
        [FromQuery] DateTime cycleStartUtc,
        [FromQuery] DateTime cycleEndUtc)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        // Query-string DateTime binding produces DateTimeKind.Unspecified regardless of
        // caller intent. Pin it to Utc explicitly so the comparison against the
        // timestamptz-stored cycle bounds is unambiguous — never trust the binder's Kind.
        var start = DateTime.SpecifyKind(cycleStartUtc, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(cycleEndUtc, DateTimeKind.Utc);

        var result = await sender.Send(
            new GenerateRiderPayoutExportCommand(start, end, adminId), ct);

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
