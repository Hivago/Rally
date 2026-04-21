using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Restaurants.Commands.UpdateHours;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Restaurants;

public class UpdateHours : IEndpoint
{
    public sealed record UpdateHoursRequest(
        TimeOnly? OpeningTime,
        TimeOnly? ClosingTime,
        bool? UseCustomSchedule,
        List<WeeklyScheduleDayInput>? WeeklySchedule);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/restaurants/me/hours", HandleAsync)
            .WithName("UpdateRestaurantHours")
            .WithTags("Restaurants")
            .WithSummary("Update opening/closing time and weekly schedule (up to 3 slots per day)")
            .RequireAuthorization("Restaurant");
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] UpdateHoursRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken ct)
    {
        var restaurantId = Guid.Parse(user.FindFirstValue("sub")!);

        var command = new UpdateRestaurantHoursCommand(
            restaurantId,
            request.OpeningTime,
            request.ClosingTime,
            request.UseCustomSchedule,
            request.WeeklySchedule);

        var result = await sender.Send(command, ct);
        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(new { message = "Hours updated." });
    }
}
