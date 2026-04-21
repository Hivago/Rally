using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateOperations;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateOperations : IEndpoint
{
    public sealed record UpdateOperationsRequest(
        bool? AutoAcceptOrders,
        int? AvgPrepTimeMins,
        bool? IsAcceptingOrders,
        decimal? MinOrderAmount,
        bool? UseCustomSchedule);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/operations", HandleAsync)
            .WithName("UpdateRestaurantOperations")
            .WithTags("Restaurants")
            .WithSummary("Toggle auto-accept, accepting orders, min order amount, prep time, custom schedule flag")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateOperationsRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantOperationsCommand(
            restaurantId,
            request.AutoAcceptOrders,
            request.AvgPrepTimeMins,
            request.IsAcceptingOrders,
            request.MinOrderAmount,
            request.UseCustomSchedule);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Operations settings updated." });
    }
}
