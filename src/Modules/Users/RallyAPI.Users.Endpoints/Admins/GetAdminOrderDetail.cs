using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetAdminOrderDetail;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetAdminOrderDetail : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/orders/{orderId:guid}", HandleAsync)
            .WithName("GetAdminOrderDetail")
            .WithTags("Admins")
            .WithSummary("Admin-scoped order detail with rider/restaurant phones, escalation, and delay")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid orderId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(new GetAdminOrderDetailQuery(adminId, orderId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
