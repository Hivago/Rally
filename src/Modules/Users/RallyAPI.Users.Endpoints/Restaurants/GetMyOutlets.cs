using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Queries.GetMyOutlets;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

/// <summary>
/// Multi-outlet switcher support: resolves the caller's restaurant_ids claim (itself plus
/// any same-owner sibling outlets) into displayable outlet summaries. For a single-outlet
/// restaurant this just returns that one outlet.
/// </summary>
public class GetMyOutlets : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/restaurants/me/outlets", HandleAsync)
            .WithName("GetMyRestaurantOutlets")
            .WithTags("Restaurants")
            .WithSummary("List the outlets (self + same-owner siblings) the current restaurant login is authorized for")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var restaurantIds = ExtractRestaurantIds(user);
        if (restaurantIds.Count == 0)
            return Results.Unauthorized();

        var result = await sender.Send(new GetMyOutletsQuery(restaurantIds), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }

    private static IReadOnlyList<Guid> ExtractRestaurantIds(ClaimsPrincipal user)
    {
        var subClaim = user.FindFirstValue("sub");
        var restaurantIdsClaim = user.FindFirstValue("restaurant_ids");

        if (string.IsNullOrEmpty(restaurantIdsClaim))
            return Guid.TryParse(subClaim, out var subId) ? new[] { subId } : Array.Empty<Guid>();

        return restaurantIdsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }
}
