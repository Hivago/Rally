using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.AddOutlet;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class AddOutlet : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurants/outlets", HandleAsync)
            .WithName("AddOutlet")
            .WithTags("Restaurants")
            .WithSummary("Add a new outlet for the restaurant owner")
            .RequireAuthorization("Restaurant");
    }

    public record AddOutletRequest(
        string Name,
        string Phone,
        string Email,
        string Password,
        string AddressLine,
        decimal Latitude,
        decimal Longitude,
        string? FssaiNumber);

    private static async Task<IResult> HandleAsync(
        AddOutletRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var restaurantId = httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        // Resolve owner ID from the current restaurant
        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(
            Guid.Parse(restaurantId), cancellationToken);

        if (restaurant?.OwnerId is null)
            return Results.BadRequest(new { error = "Restaurant is not linked to an owner account. Contact support." });

        var command = new AddOutletCommand
        {
            OwnerId = restaurant.OwnerId.Value,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Password = request.Password,
            AddressLine = request.AddressLine,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            FssaiNumber = request.FssaiNumber
        };

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/restaurants/{result.Value}", new { id = result.Value })
            : result.Error.ToErrorResult();
    }
}
