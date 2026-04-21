using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdatePassword;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdatePassword : IEndpoint
{
    public sealed record UpdatePasswordRequest(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/password", HandleAsync)
            .WithName("UpdateRestaurantPassword")
            .WithTags("Restaurants")
            .WithSummary("Change account password (requires current password)")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdatePasswordRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantPasswordCommand(
            restaurantId,
            request.CurrentPassword,
            request.NewPassword);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Password updated." });
    }
}
