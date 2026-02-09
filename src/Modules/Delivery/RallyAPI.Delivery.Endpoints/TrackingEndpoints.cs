using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Delivery.Application.DTOs;
using RallyAPI.Delivery.Application.Queries.GetTrackingInfo;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Delivery.Endpoints;

public static class TrackingEndpoints
{
    public static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/track")
            .WithTags("Tracking")
            .WithOpenApi();

        // Track by Order Number (public - no auth required)
        group.MapGet("/{orderNumber}", GetTrackingInfo)
            .WithName("GetTrackingInfo")
            .WithSummary("Get delivery tracking information")
            .Produces<TrackingDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetTrackingInfo(
        string orderNumber,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetTrackingInfoQuery { OrderNumber = orderNumber };
        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Message,
                Status = 404
            });
    }
}