using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.ResetRestaurantPassword;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class ResetRestaurantPassword : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/restaurants/{restaurantId:guid}/reset-password", HandleAsync)
            .WithName("ResetRestaurantPassword")
            .WithTags("Admins")
            .WithSummary("Force-reset a restaurant's password. Returns a new temporary password once — relay it to the restaurant out of band.")
            .RequireAuthorization("Admin");
    }

    private static async Task<IResult> HandleAsync(
        Guid restaurantId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new ResetRestaurantPasswordCommand(adminId, restaurantId);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
