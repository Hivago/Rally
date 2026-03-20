using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Menus.Commands.CreateMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Menus;

public class CreateMenu : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurant/menus", HandleAsync)
            .WithTags("Restaurant Menus")
            .WithSummary("Create a new menu")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        CreateMenuRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var command = new CreateMenuCommand(
            restaurantId,
            request.Name,
            request.Description,
            request.DisplayOrder);

        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/menus/{result.Value.MenuId}", result.Value)
            : result.Error.ToErrorResult();
    }
}

public record CreateMenuRequest(
    string Name,
    string? Description,
    int DisplayOrder);