using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.SetRestaurantStatus;

namespace RallyAPI.Users.Endpoints.Admins;

public class SetRestaurantStatus : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/admins/restaurants/{restaurantId:guid}/status", HandleAsync)
            .WithName("AdminSetRestaurantStatus")
            .WithTags("Admins")
            .WithSummary("Activate or deactivate a restaurant (admin)")
            .RequireAuthorization("Admin");
    }

    public record SetRestaurantStatusRequest(bool IsActive);

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        SetRestaurantStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SetRestaurantStatusCommand(restaurantId, request.IsActive);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToErrorResult();
    }
}
