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

        if (!Enum.TryParse<VehicleType>(request.VehicleType, ignoreCase: true, out var vehicleType))
            return Results.UnprocessableEntity(new { code = "Validation.Error", message = $"Invalid vehicle type '{request.VehicleType}'. Valid values: Bicycle, Scooter, Bike." });

        var command = new CreateRiderCommand(
            adminId,
            request.Phone,
            request.Name,
            vehicleType,
            request.VehicleNumber);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/riders/{result.Value}", new { riderId = result.Value })
            : result.Error.ToErrorResult();
    }
}
