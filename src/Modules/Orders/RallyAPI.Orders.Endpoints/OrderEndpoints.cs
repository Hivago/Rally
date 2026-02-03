using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Application.Commands.AssignRider;
using RallyAPI.Orders.Application.Commands.CancelOrder;
using RallyAPI.Orders.Application.Commands.ConfirmOrder;
using RallyAPI.Orders.Application.Commands.PlaceOrder;
using RallyAPI.Orders.Application.Commands.RejectOrder;
using RallyAPI.Orders.Application.Commands.UpdateOrderStatus;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.DTOs.Requests;
using RallyAPI.Orders.Application.Queries.GetActiveOrders;
using RallyAPI.Orders.Application.Queries.GetOrderById;
using RallyAPI.Orders.Application.Queries.GetOrderByNumber;
using RallyAPI.Orders.Application.Queries.GetOrdersByCustomer;
using RallyAPI.Orders.Application.Queries.GetOrdersByRestaurant;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Distance;
using RallyAPI.SharedKernel.Abstractions.Pricing;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Endpoints;

/// <summary>
/// Minimal API endpoints for Orders module.
/// </summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .WithOpenApi();

        // Place Order
        group.MapPost("/", PlaceOrder)
            .WithName("PlaceOrder")
            .WithSummary("Place a new order")
            .RequireAuthorization("Customer")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // Get Order by ID
        group.MapGet("/{orderId:guid}", GetOrderById)
            .WithName("GetOrderById")
            .WithSummary("Get order by ID")
            .RequireAuthorization()
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Get Order by Number
        group.MapGet("/number/{orderNumber}", GetOrderByNumber)
            .WithName("GetOrderByNumber")
            .WithSummary("Get order by order number")
            .RequireAuthorization()
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Get My Orders (Customer)
        group.MapGet("/my-orders", GetMyOrders)
            .WithName("GetMyOrders")
            .WithSummary("Get current customer's orders")
            .RequireAuthorization("Customer")
            .Produces<PagedResult<OrderSummaryDto>>();

        // Get Restaurant Orders
        group.MapGet("/restaurant/{restaurantId:guid}", GetRestaurantOrders)
            .WithName("GetRestaurantOrders")
            .WithSummary("Get orders for a restaurant")
            .RequireAuthorization("Restaurant")
            .Produces<PagedResult<OrderSummaryDto>>();

        // Get Active Orders (Admin)
        group.MapGet("/active", GetActiveOrders)
            .WithName("GetActiveOrders")
            .WithSummary("Get all active orders")
            .RequireAuthorization("Admin")
            .Produces<IReadOnlyList<OrderSummaryDto>>();

        // Confirm Order (Restaurant)
        group.MapPut("/{orderId:guid}/confirm", ConfirmOrder)
            .WithName("ConfirmOrder")
            .WithSummary("Confirm an order (restaurant)")
            .RequireAuthorization("Restaurant")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Start Preparing (Restaurant)
        group.MapPut("/{orderId:guid}/preparing", StartPreparing)
            .WithName("StartPreparing")
            .WithSummary("Mark order as preparing")
            .RequireAuthorization("Restaurant")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Ready for Pickup (Restaurant)
        group.MapPut("/{orderId:guid}/ready", MarkReadyForPickup)
            .WithName("MarkReadyForPickup")
            .WithSummary("Mark order as ready for pickup")
            .RequireAuthorization("Restaurant")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Assign Rider
        group.MapPut("/{orderId:guid}/assign-rider", AssignRider)
            .WithName("AssignRider")
            .WithSummary("Assign a rider to the order")
            .RequireAuthorization("Admin", "Restaurant")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Picked Up (Rider)
        group.MapPut("/{orderId:guid}/pickup", MarkPickedUp)
            .WithName("MarkPickedUp")
            .WithSummary("Mark order as picked up by rider")
            .RequireAuthorization("Rider")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Mark Delivered (Rider)
        group.MapPut("/{orderId:guid}/deliver", MarkDelivered)
            .WithName("MarkDelivered")
            .WithSummary("Mark order as delivered")
            .RequireAuthorization("Rider")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Cancel Order
        group.MapPut("/{orderId:guid}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancel an order")
            .RequireAuthorization()
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Reject Order (Restaurant)
        group.MapPut("/{orderId:guid}/reject", RejectOrder)
            .WithName("RejectOrder")
            .WithSummary("Reject an order (restaurant)")
            .RequireAuthorization("Restaurant")
            .Produces<OrderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);



        app.MapGet("/api/test/distance", async (
    IDistanceCalculator distanceCalculator,
    IDeliveryPricingCalculator pricingCalculator) =>
        {
            // Test: Connaught Place → India Gate (Delhi)
            var distance = await distanceCalculator.GetDistanceAsync(
                28.6315, 77.2167,  // Connaught Place
                28.6129, 77.2295); // India Gate

            var pricing = await pricingCalculator.CalculateAsync(new DeliveryPriceRequest
            {
                PickupLatitude = 28.6315,
                PickupLongitude = 77.2167,
                DropLatitude = 28.6129,
                DropLongitude = 77.2295,
                City = "Delhi",
                OrderAmount = 500
            });

            return Results.Ok(new
            {
                Distance = new
                {
                    distance.DistanceKm,
                    distance.DurationMinutes,
                    distance.DistanceText,
                    distance.DurationText,
                    distance.IsSuccess,
                    distance.ErrorMessage
                },
                Pricing = new
                {
                    pricing.FinalFee,
                    pricing.BaseFee,
                    pricing.DistanceKm,
                    pricing.EstimatedMinutes,
                    pricing.QuoteId,
                    pricing.Breakdown,
                    pricing.IsSuccess,
                    pricing.ErrorMessage
                }
            });
        })
.WithTags("Test");

        return app;
    }

    #region Endpoint Handlers

    private static async Task<IResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {

        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var command = PlaceOrderCommand.Create(
            customerId: currentUser.UserId.Value,
            customerName: currentUser.UserName ?? "Customer",
            paymentId: request.PaymentId,
            request: request,
            paymentTransactionId: request.PaymentTransactionId,
            deliveryQuoteId: request.DeliveryQuoteId,
            customerPhone: currentUser.Phone,
            customerEmail: currentUser.Email);


        //var command = PlaceOrderCommand.Create(
        //    currentUser.UserId.Value,
        //    currentUser.UserName ?? "Customer",
        //     paymentId: request.PaymentId,
        //    request,
        //    currentUser.Phone,
        //    currentUser.Email);

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/orders/{result.Value.Id}", result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetOrderById(
        Guid orderId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(orderId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetOrderByNumber(
        string orderNumber,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByNumberQuery(orderNumber), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetMyOrders(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var query = new GetOrdersByCustomerQuery
        {
            CustomerId = currentUser.UserId.Value,
            Page = page > 0 ? page : 1,
            PageSize = pageSize > 0 ? Math.Min(pageSize, 50) : 20
        };

        var result = await mediator.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetRestaurantOrders(
        Guid restaurantId,
        [FromQuery] bool activeOnly,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetOrdersByRestaurantQuery
        {
            RestaurantId = restaurantId,
            ActiveOnly = activeOnly,
            Page = page > 0 ? page : 1,
            PageSize = pageSize > 0 ? Math.Min(pageSize, 50) : 20
        };

        var result = await mediator.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> GetActiveOrders(
        [FromQuery] int limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetActiveOrdersQuery
        {
            Limit = limit > 0 ? Math.Min(limit, 100) : 50
        };

        var result = await mediator.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> ConfirmOrder(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        /// TODO: Get restaurant ID from current user's associated restaurant
        // For MVP, accept restaurant ID from user claims or require it in request
        var restaurantId = currentUser.UserId ?? Guid.Empty;

        var result = await mediator.Send(
            new ConfirmOrderCommand(orderId, restaurantId),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> StartPreparing(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand
        {
            OrderId = orderId,
            TargetStatus = OrderStatus.Preparing,
            ActorId = currentUser.UserId,
            ActorRole = "Restaurant"
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> MarkReadyForPickup(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand
        {
            OrderId = orderId,
            TargetStatus = OrderStatus.ReadyForPickup,
            ActorId = currentUser.UserId,
            ActorRole = "Restaurant"
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> AssignRider(
        Guid orderId,
        [FromBody] AssignRiderRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AssignRiderCommand
        {
            OrderId = orderId,
            RiderId = request.RiderId,
            RiderName = request.RiderName,
            RiderPhone = request.RiderPhone
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> MarkPickedUp(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand
        {
            OrderId = orderId,
            TargetStatus = OrderStatus.PickedUp,
            ActorId = currentUser.UserId,
            ActorRole = "Rider"
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> MarkDelivered(
        Guid orderId,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand
        {
            OrderId = orderId,
            TargetStatus = OrderStatus.Delivered,
            ActorId = currentUser.UserId,
            ActorRole = "Rider"
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    private static async Task<IResult> CancelOrder(
        Guid orderId,
        [FromBody] CancelOrderRequest request,
        IMediator mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand
        {
            OrderId = orderId,
            CancelledBy = currentUser.UserId ?? Guid.Empty,
            Reason = request.Reason,
            Notes = request.Notes
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    #endregion

    #region Helper Methods

    private static ProblemDetails CreateProblemDetails(Error error)
    {
        return new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = GetStatusCode(error.Code)
        };
    }

    private static int GetStatusCode(string errorCode)
    {
        return errorCode switch
        {
            var code when code.Contains("NotFound") => StatusCodes.Status404NotFound,
            var code when code.Contains("Unauthorized") => StatusCodes.Status401Unauthorized,
            var code when code.Contains("Forbidden") => StatusCodes.Status403Forbidden,
            var code when code.Contains("Validation") => StatusCodes.Status400BadRequest,
            var code when code.Contains("Conflict") => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }

    private static async Task<IResult> RejectOrder(
    Guid orderId,
    [FromBody] RejectOrderRequest request,
    IMediator mediator,
    ICurrentUserService currentUser,
    CancellationToken cancellationToken)
    {
        var restaurantId = currentUser.UserId ?? Guid.Empty;

        var command = new RejectOrderCommand
        {
            OrderId = orderId,
            RestaurantId = restaurantId,
            Reason = request.Reason
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(CreateProblemDetails(result.Error));
    }

    #endregion
}