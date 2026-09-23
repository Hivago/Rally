using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.ApproveRestaurantLocationCorrection;

namespace RallyAPI.Users.Endpoints.Admins;

public class ApproveRestaurantLocationCorrection : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/restaurant-location-reviews/{queueEntryId:guid}/approve", HandleAsync)
            .WithName("AdminApproveRestaurantLocationCorrection")
            .WithTags("Admins")
            .WithSummary("Approve a pin-drift finding: apply the suggested coordinates to the restaurant (admin)")
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
            new ApproveRestaurantLocationCorrectionCommand(queueEntryId, adminId), cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
