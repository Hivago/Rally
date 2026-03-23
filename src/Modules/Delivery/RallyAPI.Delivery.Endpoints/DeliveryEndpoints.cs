using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.Commands.GetQuote;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Endpoints.Requests;
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
}
