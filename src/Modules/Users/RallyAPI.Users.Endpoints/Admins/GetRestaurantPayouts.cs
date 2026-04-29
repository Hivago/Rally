using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayouts;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRestaurantPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/restaurant", HandleAsync)
            .WithName("GetRestaurantPayouts")
            .WithTags("Admins")
            .WithSummary("Paged restaurant payouts list with date/owner/status filters (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        DateTime? from = null,
        DateTime? to = null,
        Guid? ownerId = null,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var query = new GetRestaurantPayoutsQuery(
            adminId,
            from,
            to,
            ownerId,
            status,
            page,
            pageSize);

        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
