using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Commands.DeleteMenuItem;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class DeleteMenuItem : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/restaurant/items/{itemId:guid}", HandleAsync)
            .WithTags("Restaurant Menu Items")
            .WithSummary("Delete a menu item")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid itemId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new DeleteMenuItemCommand(itemId, restaurantId);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
