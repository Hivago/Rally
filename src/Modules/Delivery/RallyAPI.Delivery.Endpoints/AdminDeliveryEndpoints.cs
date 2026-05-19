using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.RefreshDeliveryStatus;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Delivery.Endpoints;

public static class AdminDeliveryEndpoints
{
    public static IEndpointRouteBuilder MapAdminDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deliveries")
            .WithTags("Delivery - Admin");

        group.MapPost("/orders/{orderId:guid}/refresh-status", RefreshStatus)
            .WithName("RefreshDeliveryStatus")
            .WithSummary("Pull current state from ProRouting and reconcile the Order")
            .RequireAuthorization("RestaurantOrAdmin")
            .Produces<RefreshDeliveryStatusResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RefreshStatus(
        Guid orderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userType = httpContext.User.FindFirst("user_type")?.Value ?? string.Empty;
        var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var subClaim = httpContext.User.FindFirst("sub")?.Value ?? string.Empty;
        if (!Guid.TryParse(subClaim, out var callerId))
            return Results.Unauthorized();

        var command = new RefreshDeliveryStatusCommand(orderId, callerId, isAdmin);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
