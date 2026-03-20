using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetMenuItemById;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class GetMenuItemById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/items/{itemId:guid}", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Get menu item details")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid itemId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetMenuItemByIdQuery(itemId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}