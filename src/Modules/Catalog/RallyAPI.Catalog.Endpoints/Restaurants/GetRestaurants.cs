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
            .WithSummary("Browse restaurants. Supports location, cuisine, dietary, price range, prep-time, pickup, sort, and pagination.")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        double? lat,
        double? lng,
        double? radiusKm,
        string? search,
        string? cuisines,
        bool? pureVeg,
        bool? veganFriendly,
        bool? jainOptions,
        bool? openNow,
        int? maxPrepTimeMins,
        decimal? minPrice,
        decimal? maxPrice,
        bool? supportsPickup,
        string? sort,
        int? page,
        int? pageSize,
        ISender sender,
        CancellationToken ct)
    {
        // Clamp pagination at the boundary so handlers / services don't need to guard.
        var safePage = page is null or < 1 ? 1 : page.Value;
        var safePageSize = pageSize switch
        {
            null => 20,
            < 1 => 20,
            > 100 => 100,
            _ => pageSize.Value
        };

        var query = new GetRestaurantsQuery(
            lat,
            lng,
            radiusKm,
            search,
            cuisines,
            pureVeg,
            veganFriendly,
            jainOptions,
            openNow,
            maxPrepTimeMins,
            minPrice,
            maxPrice,
            supportsPickup,
            sort,
            safePage,
            safePageSize);

        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
