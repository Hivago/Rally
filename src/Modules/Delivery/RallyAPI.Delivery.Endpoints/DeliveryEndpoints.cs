using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.CreateDeliveryRequest;
using RallyAPI.Delivery.Application.Commands.GetQuote;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Endpoints.Requests;
using RallyAPI.Orders.Application.Abstractions;
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

        // Create Delivery Request (after order confirmed)
        group.MapPost("/request", CreateDeliveryRequest)
            .WithName("CreateDeliveryRequest")
            .WithSummary("Create delivery request for an order")
            .RequireAuthorization("RestaurantOrAdmin")
            .Produces<DeliveryRequestDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Get Delivery by Order ID
        group.MapGet("/order/{orderId:guid}", GetDeliveryByOrderId)
            .WithName("GetDeliveryByOrderId")
            .WithSummary("Get delivery request by order ID")
            .RequireAuthorization()
            .Produces<DeliveryRequestDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

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
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> CreateDeliveryRequest(
        [FromBody] CreateDeliveryRequestRequest request,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var command = new CreateDeliveryRequestCommand
        {
            OrderId = request.OrderId,
            OrderNumber = request.OrderNumber,
            QuoteId = request.QuoteId,
            RequestingUserId = currentUser.UserId,
            IsAdmin = currentUser.IsAdmin,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            PickupPincode = request.PickupPincode,
            PickupAddress = request.PickupAddress,
            PickupContactName = request.PickupContactName,
            PickupContactPhone = request.PickupContactPhone,
            DropLatitude = request.DropLatitude,
            DropLongitude = request.DropLongitude,
            DropPincode = request.DropPincode,
            DropAddress = request.DropAddress,
            DropContactName = request.DropContactName,
            DropContactPhone = request.DropContactPhone,
            ItemCount = request.ItemCount
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/delivery/{result.Value.Id}", result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetDeliveryByOrderId(
        Guid orderId,
        IMediator mediator,
        CancellationToken ct)
    {
        // TODO: Add GetDeliveryByOrderIdQuery
        return Results.Ok();
    }

    private static ProblemDetails CreateProblemDetails(Error error) => new()
    {
        Title = error.Code,
        Detail = error.Message,
        Status = error.Code switch
        {
            var code when code.Contains("NotFound") => 404,
            var code when code.Contains("Forbidden") => 403,
            var code when code.Contains("Unauthorized") => 401,
            var code when code.Contains("Conflict") => 409,
            _ => 400
        }
    };
}
