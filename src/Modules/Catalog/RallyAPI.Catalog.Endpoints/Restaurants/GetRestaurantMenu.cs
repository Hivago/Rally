// File: src/Modules/Catalog/RallyAPI.Catalog.Endpoints/Restaurants/GetRestaurantMenu.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantMenu;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class GetRestaurantMenu : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/restaurants/{restaurantId:guid}/menu", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Get full menu with items and options for a restaurant")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetRestaurantMenuQuery(restaurantId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}