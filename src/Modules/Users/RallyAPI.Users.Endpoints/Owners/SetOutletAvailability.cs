using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.SetOutletAvailability;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class SetOutletAvailability : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/owners/me/outlets/{restaurantId:guid}/availability", HandleAsync)
            .WithName("SetOwnerOutletAvailability")
            .WithTags("Owners")
            .WithSummary("Owner toggles a specific outlet online/offline")
            .RequireAuthorization("Owner");
    }

    public record SetOutletAvailabilityRequest(bool IsAcceptingOrders);

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        SetOutletAvailabilityRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new SetOutletAvailabilityCommand(ownerId, restaurantId, request.IsAcceptingOrders);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { restaurantId, isAcceptingOrders = request.IsAcceptingOrders });
    }
}
