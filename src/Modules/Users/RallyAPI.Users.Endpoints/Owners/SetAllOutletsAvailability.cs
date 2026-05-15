using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Owners.Commands.SetAllOutletsAvailability;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Owners;

public class SetAllOutletsAvailability : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/owners/me/outlets/availability", HandleAsync)
            .WithName("SetOwnerAllOutletsAvailability")
            .WithTags("Owners")
            .WithSummary("Owner toggles all owned outlets online/offline. Inactive outlets are skipped when going online.")
            .RequireAuthorization("Owner");
    }

    public record SetAllOutletsAvailabilityRequest(bool IsAcceptingOrders);

    private static async Task<IResult> HandleAsync(
        SetAllOutletsAvailabilityRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new SetAllOutletsAvailabilityCommand(ownerId, request.IsAcceptingOrders);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
