using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.RejectRestaurantLocationCorrection;

namespace RallyAPI.Users.Endpoints.Admins;

public class RejectRestaurantLocationCorrection : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/restaurant-location-reviews/{queueEntryId:guid}/reject", HandleAsync)
            .WithName("AdminRejectRestaurantLocationCorrection")
            .WithTags("Admins")
            .WithSummary("Reject a pin-drift finding: leave the restaurant's coordinates unchanged (admin)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid queueEntryId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);
        var result = await sender.Send(
            new RejectRestaurantLocationCorrectionCommand(queueEntryId, adminId), cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
