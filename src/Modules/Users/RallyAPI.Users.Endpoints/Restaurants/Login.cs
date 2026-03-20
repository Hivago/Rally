using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.Login;
using Microsoft.AspNetCore.Builder;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/restaurants/login", HandleAsync)
            .WithName("RestaurantLogin")
            .WithTags("Restaurants")
            .AllowAnonymous()
            .RequireRateLimiting("login");
    }

    public record RestaurantLoginRequest(string Email, string Password);

    private static async Task<IResult> HandleAsync(
        RestaurantLoginRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new LoginRestaurantCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }
}