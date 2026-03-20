using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.ToggleMenuItemAvailability;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class ToggleMenuItemAvailability : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurant/items/{itemId:guid}/availability", HandleAsync)
            .WithTags("Restaurant Menu Items")
            .WithSummary("Toggle menu item availability")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid itemId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new ToggleMenuItemAvailabilityCommand(itemId, restaurantId);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}