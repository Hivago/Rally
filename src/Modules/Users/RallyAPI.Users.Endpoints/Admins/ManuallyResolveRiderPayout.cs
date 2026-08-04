using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Application.Admins.Commands.ManuallyResolveRiderPayout;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Manually resolves a Processing rider payout. Mirrors ManuallyResolveRestaurantPayout.
/// </summary>
public class ManuallyResolveRiderPayout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/payouts/rider/{payoutId:guid}/manual-resolve", HandleAsync)
            .WithName("ManuallyResolveRiderPayout")
            .WithTags("Admins")
            .WithSummary("Manually mark a stuck Processing rider payout Paid or Failed (admin panel, Super Admin only)")
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
            new ManuallyResolveRiderPayoutCommand(
                payoutId, request.Outcome, request.TransactionReference, request.Reason, adminId), ct);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
