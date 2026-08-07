using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.ListApplications;
using RallyAPI.Marketing.Domain.Enums;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.RestaurantOnboarding;

/// <summary>
/// Admin review list — always masked (no decrypted bank/PAN/GST here). Full decryption only
/// ever happens in GetRestaurantOnboardingApplicationDetail, and only for Super Admin.
/// </summary>
public sealed class ListRestaurantOnboardingApplications : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/restaurant-onboarding", HandleAsync)
            .WithName("AdminListRestaurantOnboardingApplications")
            .WithTags("Marketing")
            .WithSummary("Admin: paginated list of restaurant onboarding applications (masked sensitive fields).")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken cancellationToken,
        [FromQuery] OnboardingApplicationStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(
            new ListRestaurantOnboardingApplicationsQuery(status, search, page, pageSize), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
