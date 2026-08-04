using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Commands.ManuallyResolveRestaurantPayout;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Manually resolves a Processing restaurant payout the automatic reconcile matcher couldn't
/// (ambiguous match, drifted bank details, a row the bank's report never covered). Same
/// Super-Admin-only trust boundary as ReconcileRestaurantPayouts — see
/// specs/icici-manual-payout-export.md section 4a.
/// </summary>
public class ManuallyResolveRestaurantPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/restaurant/{payoutId:guid}/manual-resolve", HandleAsync)
            .WithName("ManuallyResolveRestaurantPayout")
            .WithTags("Admins")
            .WithSummary("Manually mark a stuck Processing restaurant payout Paid or Failed (admin panel, Super Admin only)")
            .RequireAuthorization("Admin");
    }

    public record ManualResolveRequest(
        ManualPayoutResolutionOutcome Outcome,
        string? TransactionReference,
        string Reason);

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ManualResolveRequest request,
        ClaimsPrincipal user,
        ISender sender,
        IAdminRepository adminRepository,
        CancellationToken ct)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var admin = await adminRepository.GetByIdAsync(adminId, ct);
        if (admin is null || admin.Role != AdminRole.SuperAdmin)
            return Results.Json(
                new { error = "Only a Super Admin can manually resolve a payout — this action can flip it to Paid." },
                statusCode: StatusCodes.Status403Forbidden);

        var result = await sender.Send(
            new ManuallyResolveRestaurantPayoutCommand(
                payoutId, request.Outcome, request.TransactionReference, request.Reason, adminId), ct);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
