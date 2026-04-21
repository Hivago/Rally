using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateBasics;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateBasics : IEndpoint
{
    public sealed record UpdateBasicsRequest(
        string? Name,
        string? Phone,
        string? Email,
        string? FssaiNumber,
        string? AddressLine,
        decimal? Latitude,
        decimal? Longitude,
        string? Description);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/profile", HandleAsync)
            .WithName("UpdateRestaurantBasics")
            .WithTags("Restaurants")
            .WithSummary("Update restaurant basics: name, phone, email, FSSAI, address, description")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateBasicsRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantBasicsCommand(
            restaurantId,
            request.Name,
            request.Phone,
            request.Email,
            request.FssaiNumber,
            request.AddressLine,
            request.Latitude,
            request.Longitude,
            request.Description);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Profile updated successfully." });
    }
}
