using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.GetQuote;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Application.Queries.GetDeliveryCodes;
using RallyAPI.Delivery.Endpoints.Requests;
using RallyAPI.SharedKernel.Abstractions;
using RallyAPI.SharedKernel.Extensions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Endpoints;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery")
            .WithTags("Delivery")
            .WithOpenApi();

        // Get Quote (at checkout)
        group.MapPost("/quote", GetQuote)
            .WithName("GetDeliveryQuote")
            .WithSummary("Get delivery quote at checkout")
            .Produces<DeliveryQuoteDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Get OTP codes for an order (role-filtered: customer sees DropCode,
        // restaurant sees PickupCode, admin sees both)
        group.MapGet("/orders/{orderId:guid}/codes", GetDeliveryCodes)
            .WithName("GetDeliveryCodes")
            .WithSummary("Get pickup/drop OTP codes for an order")
            .RequireAuthorization()
            .Produces<DeliveryCodesDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetQuote(
        [FromBody] GetQuoteRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new GetQuoteCommand
        {
            RestaurantId = request.RestaurantId,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            PickupPincode = request.PickupPincode,
            DropLatitude = request.DropLatitude,
            DropLongitude = request.DropLongitude,
            DropPincode = request.DropPincode,
            City = request.City,
            OrderAmount = request.OrderAmount
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }

    private static async Task<IResult> GetDeliveryCodes(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Results.Unauthorized();

        var role = currentUser.IsAdmin ? "Admin"
            : currentUser.IsRestaurant ? "Restaurant"
            : currentUser.IsRider ? "Rider"
            : currentUser.IsCustomer ? "Customer"
            : string.Empty;

        var result = await mediator.Send(
            new GetDeliveryCodesQuery(orderId, currentUser.UserId.Value, role), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToErrorResult();
    }
}
