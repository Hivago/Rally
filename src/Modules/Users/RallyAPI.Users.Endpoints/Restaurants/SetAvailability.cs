using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.SetAvailability;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class SetAvailability : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/restaurants/me/availability", HandleAsync)
            .WithName("SetRestaurantAvailability")
            .WithTags("Restaurants")
            .WithSummary("Restaurant marks itself as open (accepting orders) or closed")
            .RequireAuthorization("Restaurant");
    }

    public record SetAvailabilityRequest(bool IsAcceptingOrders);

    private static async Task<IResult> HandleAsync(
        SetAvailabilityRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new SetRestaurantAvailabilityCommand(restaurantId, request.IsAcceptingOrders);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { isAcceptingOrders = request.IsAcceptingOrders });
    }
}
