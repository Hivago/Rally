using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Queries.GetDetails;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class GetDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/restaurants/me/details", HandleAsync)
            .WithName("GetRestaurantDetails")
            .WithTags("Restaurants")
            .WithSummary("Get full restaurant settings (profile, dietary, operations, hours, delivery, notifications)")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var result = await sender.Send(new GetRestaurantDetailsQuery(restaurantId), cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}
