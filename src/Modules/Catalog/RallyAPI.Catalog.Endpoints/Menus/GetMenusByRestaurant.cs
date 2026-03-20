using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Menus.Queries.GetMenusByRestaurant;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Menus;

public class GetMenusByRestaurant : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/restaurants/{restaurantId:guid}/menus", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Get all menus for a restaurant")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetMenusByRestaurantQuery(restaurantId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}