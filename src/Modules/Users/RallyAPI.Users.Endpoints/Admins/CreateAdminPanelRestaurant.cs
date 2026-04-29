using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.CreateAdminPanelRestaurant;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Admin-panel restaurant creation. Singular path is the new convention for admin-panel
/// endpoints. Owner is required; rstCode is generated server-side.
/// Legacy POST /api/admins/restaurants stays for backward compat.
/// </summary>
public class CreateAdminPanelRestaurant : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/restaurants", HandleAsync)
            .WithName("CreateAdminPanelRestaurant")
            .WithTags("Admins")
            .WithSummary("Create a restaurant from the admin panel (requires existing owner)")
            .RequireAuthorization("Admin");
    }

    public record CreateAdminPanelRestaurantRequest(
        Guid OwnerId,
        string Name,
        string Phone,
        string Email,
        string Password,
        string AddressLine,
        decimal Latitude,
        decimal Longitude);

    private static async Task<IResult> HandleAsync(
        CreateAdminPanelRestaurantRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateAdminPanelRestaurantCommand(
            adminId,
            request.OwnerId,
            request.Name,
            request.Phone,
            request.Email,
            request.Password,
            request.AddressLine,
            request.Latitude,
            request.Longitude);

        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Created(
                $"/api/admin/restaurants/{result.Value.RestaurantId}",
                new { restaurantId = result.Value.RestaurantId, rstCode = result.Value.RstCode });
    }
}
