using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RallyAPI.Integrations.ProRouting.Models;
using MediatR;
using RallyAPI.Delivery.Application.Commands.TriggerDispatch;

namespace RallyAPI.Delivery.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Webhooks")
            .WithOpenApi();

        // ProRouting Webhook
        group.MapPost("/prorouting", HandleProRoutingWebhook)
            .WithName("ProRoutingWebhook")
            .WithSummary("Handle ProRouting status updates")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> HandleProRoutingWebhook(
        [FromBody] ProRoutingWebhookPayload payload,
        [FromHeader(Name = "x-pro-api-key")] string? apiKey,
        Microsoft.Extensions.Options.IOptions<RallyAPI.Integrations.ProRouting.ProRoutingOptions> options,
        DeliveryDbContext dbContext,
        ISender sender,
        ILogger<ProRoutingWebhookPayload> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey != options.Value.ApiKey)
        {
            logger.LogWarning("Unauthorized ProRouting webhook access attempt.");
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "ProRouting webhook received: OrderId={OrderId}, State={State}",
            payload.OrderId, payload.State);

        // Find delivery by external task ID
        var delivery = await dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.ExternalTaskId == payload.OrderId, ct);

        if (delivery is null)
        {
            // Try by client order ID
            delivery = await dbContext.DeliveryRequests
                .FirstOrDefaultAsync(r => r.OrderNumber == payload.ClientOrderId, ct);
        }

        if (delivery is null)
        {
            logger.LogWarning(
                "Delivery not found for ProRouting webhook: {OrderId}",
                payload.OrderId);
            return Results.Ok(new { message = "Delivery not found, ignored" });
        }

        try
        {
            // Update based on state
            switch (payload.State?.ToLower())
            {
                case "agent-assigned":
                case "agent_assigned":
                    delivery.Update3PLRiderInfo(
                        payload.Agent?.Name,
                        payload.Agent?.Phone,
                        payload.TrackingUrl);
                    if (delivery.Status == DeliveryRequestStatus.Searching3PL)
                    {
                        delivery.Assign3PLRider(
                            payload.OrderId,
                            "ProRouting",
                            payload.Agent?.Name,
                            payload.Agent?.Phone,
                            payload.TrackingUrl,
                            delivery.QuotedPrice); // Base price initially
                    }
                    break;

                case "picked-up":
                case "picked_up":
                case "order-picked-up":
                    delivery.MarkPickedUp();
                    break;

                case "delivered":
                case "order-delivered":
                    delivery.MarkDelivered();
                    break;

                case "cancelled":
                case "failed":
                case "rto-initiated":
                    if (delivery.Status == DeliveryRequestStatus.Searching3PL || delivery.Status == DeliveryRequestStatus.Assigned3PL)
                    {
                        logger.LogInformation("3PL failed for delivery {DeliveryId}. Triggering Own Fleet fallback via background command.", delivery.Id);
                        
                        // Clean up 3PL variables and transition state so orchestrator tries Own Fleet
                        delivery.TransitionToOwnFleetSearch();
                        await dbContext.SaveChangesAsync(ct);
                        
                        // Fire-and-forget background processing command
                        _ = sender.Send(new TriggerDispatchCommand { DeliveryRequestId = delivery.Id }, ct);
                        return Results.Ok(new { message = "Webhook processed, Own Fleet fallback triggered" });
                    }
                    else
                    {
                        delivery.MarkFailed(
                            DeliveryFailureReason.ThirdPartyFailed,
                            payload.CancelReason ?? payload.State);
                    }
                    break;

                default:
                    logger.LogDebug(
                        "Unhandled ProRouting state: {State}",
                        payload.State);
                    break;
            }

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Delivery {DeliveryId} updated to status {Status}",
                delivery.Id, delivery.Status);

            return Results.Ok(new { message = "Webhook processed" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                "Invalid state transition for delivery {DeliveryId}: {Error}",
                delivery.Id, ex.Message);

            return Results.Ok(new { message = "State transition ignored", error = ex.Message });
        }
    }
}