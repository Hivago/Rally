using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.ReconcileRiderPayouts;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Uploads ICICI's Consolidated Status Report for a rider payout export batch and applies the
/// result: Processing→Paid (with UTR) or Processing→Failed (with reason) per row. Mirrors
/// ReconcileRestaurantPayouts — same Super Admin-only trust boundary
/// (specs/icici-manual-payout-export.md section 4a).
/// </summary>
public class ReconcileRiderPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/reconcile", HandleAsync)
            .WithName("ReconcileRiderPayouts")
            .WithTags("Admins")
            .WithSummary("Upload the ICICI result file for a rider payout export batch (admin panel, Super Admin only)")
            .DisableAntiforgery()
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] Guid batchId,
        IFormFile file,
        ClaimsPrincipal user,
        ISender sender,
        IAdminRepository adminRepository,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "ICICI result file (xlsx) is required" });

        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var admin = await adminRepository.GetByIdAsync(adminId, ct);
        if (admin is null || admin.Role != AdminRole.SuperAdmin)
            return Results.Json(
                new { error = "Only a Super Admin can reconcile payouts — this action flips payouts to Paid." },
                statusCode: StatusCodes.Status403Forbidden);

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var fileBytes = buffer.ToArray();

        using var parseStream = new MemoryStream(fileBytes);
        var result = await sender.Send(
            new ReconcileRiderPayoutsCommand(batchId, parseStream, fileBytes, adminId), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
