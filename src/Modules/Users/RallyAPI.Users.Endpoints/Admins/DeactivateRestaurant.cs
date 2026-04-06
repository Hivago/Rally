using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.DeactivateRestaurant;

namespace RallyAPI.Users.Endpoints.Admins;

public class DeactivateRestaurant : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admins/restaurants/{restaurantId:guid}/deactivate", HandleAsync)
            .WithName("AdminDeactivateRestaurant")
            .WithTags("Admins")
            .WithSummary("Deactivate a restaurant (admin)")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateRestaurantCommand(restaurantId);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
