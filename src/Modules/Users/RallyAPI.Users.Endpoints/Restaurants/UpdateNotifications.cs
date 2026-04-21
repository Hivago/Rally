using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateNotifications;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateNotifications : IEndpoint
{
    public sealed record UpdateNotificationsRequest(
        bool? EmailAlerts,
        bool? BrowserNotifications,
        bool? OrderSound);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/notifications", HandleAsync)
            .WithName("UpdateRestaurantNotifications")
            .WithTags("Restaurants")
            .WithSummary("Toggle email alerts, browser notifications, order sound")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateNotificationsRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantNotificationsCommand(
            restaurantId,
            request.EmailAlerts,
            request.BrowserNotifications,
            request.OrderSound);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Notification preferences updated." });
    }
}
