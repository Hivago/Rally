using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Abstractions.Geocoding;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Lets admin find a restaurant's Google Place ID by name/address so it can be
/// linked via EditRestaurant. Reuses the same Places Autocomplete call the
/// customer address flow uses — it already returns businesses, not just addresses.
/// </summary>
public class SearchGooglePlaces : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins/places/search", HandleAsync)
            .WithName("AdminSearchGooglePlaces")
            .WithTags("Admins")
            .WithSummary("Search Google Places by name/address to find a restaurant's Place ID")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        string query,
        IGeocodingService geocodingService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Results.BadRequest(new { error = "Query must be at least 2 characters" });

        var suggestions = await geocodingService.AutocompleteAsync(
            query, maxResults: 5, ct: cancellationToken);

        return Results.Ok(new { suggestions });
    }
}
