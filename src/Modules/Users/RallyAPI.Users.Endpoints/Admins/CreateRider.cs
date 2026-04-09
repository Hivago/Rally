using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.Users.Application.Admins.Commands.CreateRider;
using RallyAPI.Users.Domain.Enums;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

public class CreateRider : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admins/riders", HandleAsync)
            .WithName("CreateRider")
            .WithTags("Admins")
            .RequireAuthorization("Admin");
    }

    public record CreateRiderRequest(
        string Phone,
        string Name,
        string VehicleType,
        string? VehicleNumber);

    private static async Task<IResult> HandleAsync(
        CreateRiderRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        if (!Enum.TryParse<VehicleType>(request.VehicleType, true, out var vehicleType))
            return Results.BadRequest(new { error = "Invalid vehicle type. Use: Bicycle, Bike, Scooter, Car, Auto" });

        var command = new CreateRiderCommand(
            adminId,
            request.Phone,
            request.Name,
            vehicleType,
            request.VehicleNumber);

        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Created($"/api/riders/{result.Value}", new { riderId = result.Value });
    }
}
