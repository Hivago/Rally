using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.ReviewApplication;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.RestaurantOnboarding;

/// <summary>
/// Approve/reject just record a review decision — no live owner/restaurant account is
/// created and no money moves, so these are plain Admin (not Super Admin). Creating the real
/// account after approval is a deliberate separate manual step.
/// </summary>
public sealed class ApproveRestaurantOnboardingApplication : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/restaurant-onboarding/{applicationId:guid}/approve", HandleAsync)
            .WithName("AdminApproveRestaurantOnboardingApplication")
            .WithTags("Marketing")
            .WithSummary("Admin: approve a Pending onboarding application.")
            .RequireAuthorization("Admin");
    }

    public sealed record ApproveRequest(string? Notes);

    private static async Task<IResult> HandleAsync(
        Guid applicationId,
        ApproveRequest? request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new ApproveRestaurantOnboardingApplicationCommand(applicationId, adminId, request?.Notes),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToErrorResult();
    }
}

public sealed class RejectRestaurantOnboardingApplication : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/restaurant-onboarding/{applicationId:guid}/reject", HandleAsync)
            .WithName("AdminRejectRestaurantOnboardingApplication")
            .WithTags("Marketing")
            .WithSummary("Admin: reject a Pending onboarding application.")
            .RequireAuthorization("Admin");
    }

    public sealed record RejectRequest(string Reason);

    private static async Task<IResult> HandleAsync(
        Guid applicationId,
        RejectRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new RejectRestaurantOnboardingApplicationCommand(applicationId, adminId, request.Reason),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToErrorResult();
    }
}
