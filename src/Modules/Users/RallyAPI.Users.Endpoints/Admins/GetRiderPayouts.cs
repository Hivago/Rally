using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRiderPayouts;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRiderPayouts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/rider", HandleAsync)
            .WithName("GetRiderPayouts")
            .WithTags("Admins")
            .WithSummary("Paged rider payouts list with date/rider/status filters (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        DateTime? from = null,
        DateTime? to = null,
        Guid? riderId = null,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        Guid adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var query = new GetRiderPayoutsQuery(
            adminId,
            from,
            to,
            riderId,
            status,
            page,
            pageSize);

        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
