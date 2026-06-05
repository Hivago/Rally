using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.PushOtpsToProvider;
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

        group.MapPost("/orders/{orderId:guid}/push-otps", PushOtps)
            .WithName("PushDeliveryOtps")
            .WithSummary("Push pickup/drop OTPs to ProRouting and mark task ready (unsticks UnFulfilled tasks)")
            .RequireAuthorization("RestaurantOrAdmin")
            .Produces<PushOtpsToProviderResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
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
        var (callerId, isAdmin, unauthorized) = ExtractCaller(httpContext);
        if (unauthorized) return Results.Unauthorized();

        var command = new RefreshDeliveryStatusCommand(orderId, callerId, isAdmin);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }

    private static async Task<IResult> PushOtps(
        Guid orderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, isAdmin, unauthorized) = ExtractCaller(httpContext);
        if (unauthorized) return Results.Unauthorized();

        var command = new PushOtpsToProviderCommand(orderId, callerId, isAdmin);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }

    private static (Guid callerId, bool isAdmin, bool unauthorized) ExtractCaller(HttpContext httpContext)
    {
        var userType = httpContext.User.FindFirst("user_type")?.Value ?? string.Empty;
        var isAdmin = userType.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var subClaim = httpContext.User.FindFirst("sub")?.Value ?? string.Empty;
        if (!Guid.TryParse(subClaim, out var callerId))
            return (Guid.Empty, false, true);

        return (callerId, isAdmin, false);
    }
}
