using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Queries.CheckDelivery;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class CheckDelivery : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/restaurants/{restaurantId:guid}/delivery-check", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Check whether a restaurant can deliver to the given coordinates")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        double lat,
        double lng,
        ISender sender,
        CancellationToken ct)
    {
        var query = new CheckDeliveryQuery(restaurantId, lat, lng);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
