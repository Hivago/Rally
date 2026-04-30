using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.CreateRestaurant;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class CreateRestaurant : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/restaurants", HandleAsync)
            .WithName("CreateRestaurant")
            .WithTags("Admins")
            .RequireAuthorization("Admin");
    }

    public record CreateRestaurantRequest(
        string Name,
        string Phone,
        string Email,
        string Password,
        string AddressLine,
        decimal Latitude,
        decimal Longitude);

    private static async Task<IResult> HandleAsync(
        CreateRestaurantRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new CreateRestaurantCommand(
            adminId,
            request.Name,
            request.Phone,
            request.Email,
            request.Password,
            request.AddressLine,
            request.Latitude,
            request.Longitude);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/restaurants/{result.Value}", new { restaurantId = result.Value })
            : result.Error.ToErrorResult();
    }
}
