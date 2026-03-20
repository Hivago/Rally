using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Menus.Commands.DeleteMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Menus;

public class DeleteMenu : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/restaurant/menus/{menuId:guid}", HandleAsync)
            .WithTags("Restaurant Menus")
            .WithSummary("Delete a menu")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid menuId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new DeleteMenuCommand(menuId, restaurantId);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Menu deleted successfully" })
            : result.Error.ToErrorResult();
    }
}