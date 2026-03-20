// File: src/Modules/Catalog/RallyAPI.Catalog.Endpoints/Restaurants/GetRestaurants.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Queries.GetRestaurants;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class GetRestaurants : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/restaurants", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("List active restaurants, optionally filtered by location")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        double? lat,
        double? lng,
        double? radiusKm,
        ISender sender,
        CancellationToken ct)
    {
        var query = new GetRestaurantsQuery(lat, lng, radiusKm);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}