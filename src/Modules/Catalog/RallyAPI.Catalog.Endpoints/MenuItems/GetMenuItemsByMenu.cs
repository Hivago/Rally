using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemsByMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class GetMenuItemsByMenu : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/menus/{menuId:guid}/items", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Get all items for a menu")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid menuId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetMenuItemsByMenuQuery(menuId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}