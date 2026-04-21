using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateDietary;
using RallyAPI.Users.Domain.Enums;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateDietary : IEndpoint
{
    public sealed record UpdateDietaryRequest(
        DietaryType? DietaryType,
        bool? IsVeganFriendly,
        bool? HasJainOptions,
        List<string>? CuisineTypes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/dietary", HandleAsync)
            .WithName("UpdateRestaurantDietary")
            .WithTags("Restaurants")
            .WithSummary("Update dietary type, vegan/Jain flags, and cuisine types")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateDietaryRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantDietaryCommand(
            restaurantId,
            request.DietaryType,
            request.IsVeganFriendly,
            request.HasJainOptions,
            request.CuisineTypes);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Dietary settings updated." });
    }
}
