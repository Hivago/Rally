// File: src/Modules/Catalog/RallyAPI.Catalog.Endpoints/Restaurants/SearchMenuItems.cs

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Catalog.Application.Restaurants.Queries.SearchMenuItems;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Catalog.Endpoints.Restaurants;

public class SearchMenuItems : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/search", HandleAsync)
            .WithTags("Customer Catalog")
            .WithSummary("Search menu items by name across all restaurants")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        string q,
        int? maxResults,
        ISender sender,
        CancellationToken ct)
    {
        var query = new SearchMenuItemsQuery(q, maxResults ?? 20);
        var result = await sender.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}