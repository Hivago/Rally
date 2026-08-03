using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Queries.GetRestaurantPayoutDetail;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class GetRestaurantPayoutDetail : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/payouts/restaurant/{payoutId:guid}", HandleAsync)
            .WithName("GetAdminRestaurantPayoutDetail")
            .WithTags("Admins")
            .WithSummary("Order-level breakdown for a single restaurant payout (admin panel)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid payoutId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var result = await sender.Send(
            new GetRestaurantPayoutDetailQuery(adminId, payoutId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
