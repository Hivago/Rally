using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetAdminOrders;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetAdminOrders : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/orders", HandleAsync)
            .WithName("GetAdminOrders")
            .WithTags("Admins")
            .WithSummary("Paged orders list with status/search/date filters and tab counts (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        string? status = "all",
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var tab = ParseTab(status);

        var query = new GetAdminOrdersQuery(
            adminId,
            tab,
            search,
            from,
            to,
            page,
            pageSize);

        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }

    private static AdminOrdersTab ParseTab(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "active" => AdminOrdersTab.Active,
            "escalated" => AdminOrdersTab.Escalated,
            "failed" => AdminOrdersTab.Failed,
            _ => AdminOrdersTab.All
        };
}
