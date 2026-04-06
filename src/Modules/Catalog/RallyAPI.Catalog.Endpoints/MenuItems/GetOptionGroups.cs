using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.MenuItems.Queries.GetOptionGroups;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.MenuItems;

public class GetOptionGroups : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/restaurant/items/{menuItemId:guid}/option-groups", HandleAsync)
            .WithTags("Option Groups")
            .WithSummary("Get all option groups for a menu item")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        Guid menuItemId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetOptionGroupsQuery(menuItemId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
