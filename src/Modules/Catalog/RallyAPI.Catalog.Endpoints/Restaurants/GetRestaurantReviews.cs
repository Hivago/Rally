// File: src/Modules/Catalog/RallyAPI.Catalog.Endpoints/Restaurants/GetRestaurantReviews.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurantReviews;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class GetRestaurantReviews : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/restaurants/{restaurantId:guid}/reviews", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Get cached Google rating and review snippets for a restaurant")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetRestaurantReviewsQuery(restaurantId);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
