using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.GetApplicationDetail;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Marketing.Endpoints.RestaurantOnboarding;

/// <summary>
/// Admin detail view. Any admin can view an application, but the decrypted bank account
/// number / PAN / GST are only included when the caller is Super Admin — this is the trust
/// boundary (mirrors the payout reconcile endpoints; see
/// docs/icici-payout-reconciliation-rules.md rule #2 for the same pattern). Non-Super-Admin
/// callers see the masked fields only, never a 403 — viewing the application itself is fine
/// for any admin reviewing it, only the raw financial PII is gated.
/// </summary>
public sealed class GetRestaurantOnboardingApplicationDetail : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/restaurant-onboarding/{applicationId:guid}", HandleAsync)
            .WithName("AdminGetRestaurantOnboardingApplicationDetail")
            .WithTags("Marketing")
            .WithSummary("Admin: view an onboarding application (decrypted bank/PAN/GST for Super Admin only).")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid applicationId,
        ClaimsPrincipal user,
        ISender sender,
        IAdminRepository adminRepository,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var admin = await adminRepository.GetByIdAsync(adminId, cancellationToken);
        var isSuperAdmin = admin is not null && admin.Role == AdminRole.SuperAdmin;

        var result = await sender.Send(
            new GetRestaurantOnboardingApplicationDetailQuery(applicationId, isSuperAdmin), cancellationToken);

        if (result.IsFailure)
            return result.Error.ToErrorResult();

        return result.Value is null
            ? Results.NotFound()
            : Results.Ok(result.Value);
    }
}
