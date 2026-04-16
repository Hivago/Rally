using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.SetMenuItemAvailability;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class SetMenuItemAvailability : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurant/items/{itemId:guid}/availability", HandleAsync)
            .WithTags("Restaurant Menu Items")
            .WithSummary("Set menu item availability (in stock / out of stock)")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid itemId,
        SetMenuItemAvailabilityRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new SetMenuItemAvailabilityCommand(itemId, restaurantId, request.IsAvailable);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}

public record SetMenuItemAvailabilityRequest(bool IsAvailable);
