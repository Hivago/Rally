using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateDelivery;
using RallyAPI.Users.Domain.Enums;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateDelivery : IEndpoint
{
    public sealed record UpdateDeliveryRequest(DeliveryMode DeliveryMode);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/delivery", HandleAsync)
            .WithName("UpdateRestaurantDeliveryMode")
            .WithTags("Restaurants")
            .WithSummary("Switch between Hivago delivery and self-delivery")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateDeliveryRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantDeliveryCommand(restaurantId, request.DeliveryMode);
        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { deliveryMode = request.DeliveryMode });
    }
}
