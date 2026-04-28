using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetLiveOrders;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetLiveOrders : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/orders/live", HandleAsync)
            .WithName("GetAdminLiveOrders")
            .WithTags("Admins")
            .WithSummary("Live feed of orders currently in active states (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        int limit = 200)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetLiveOrdersQuery(adminId, limit), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
