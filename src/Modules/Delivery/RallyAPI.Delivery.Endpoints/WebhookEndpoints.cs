using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        HttpRequest request,
        Microsoft.Extensions.Configuration.IConfiguration config,
        DeliveryDbContext dbContext,
        RallyAPI.Infrastructure.Persistence.AuditDbContext auditDb,
        RallyAPI.SharedKernel.Infrastructure.RedisIdempotencyService idempotencyService,
        ISender sender,
        ILogger<ProRoutingWebhookPayload> logger,
        CancellationToken ct)
    {
        var sourceIp = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var auditLog = new RallyAPI.SharedKernel.Domain.Entities.WebhookAuditLog
        {
            Id = Guid.NewGuid(),
            Source = "prorouting",
            ReceivedAt = DateTimeOffset.UtcNow,
            SourceIp = sourceIp,
            CorrelationId = Guid.NewGuid()
        };

        // 1. Extract Headers
        if (!request.Headers.TryGetValue("X-ProRouting-Signature", out var signatureVals) ||
            !request.Headers.TryGetValue("X-ProRouting-Timestamp", out var timestampVals) ||
            !request.Headers.TryGetValue("X-ProRouting-Event-Id", out var eventIdVals))
        {
            auditLog.ProcessingStatus = "rejected_missing_headers";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }

        var providedSignature = signatureVals.ToString();
        var timestampStr = timestampVals.ToString();
        var eventId = eventIdVals.ToString();
        
        auditLog.EventId = eventId;

        // 2. Read body
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0; // Reset for downstream 
        auditLog.RawBody = rawBody; // in a real system we'd encrypt this at rest

        // 3. Verify Timestamp
        if (!long.TryParse(timestampStr, out var timestampSecs))
        {
            auditLog.ProcessingStatus = "rejected_timestamp_invalid";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }

        var timestampDate = DateTimeOffset.FromUnixTimeSeconds(timestampSecs);
        var toleranceSecs = config.GetValue<int>("WEBHOOK_PROROUTING_TIMESTAMP_TOLERANCE_SECONDS", 300);
        
        if (Math.Abs((DateTimeOffset.UtcNow - timestampDate).TotalSeconds) > toleranceSecs)
        {
            auditLog.ProcessingStatus = "rejected_timestamp";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }
        auditLog.TimestampValid = true;

        // 4. Verify HMAC using Current and Previous secrets
        var secretCurrent = config.GetValue<string>("WEBHOOK_PROROUTING_SECRET_CURRENT") ?? "";
        var secretPrevious = config.GetValue<string>("WEBHOOK_PROROUTING_SECRET_PREVIOUS") ?? "";

        var payloadToHash = timestampStr + "." + rawBody; // Standard composition
        var signatureValid = VerifyHmac(payloadToHash, providedSignature, secretCurrent) ||
                             VerifyHmac(payloadToHash, providedSignature, secretPrevious);

        if (!signatureValid)
        {
            auditLog.SignatureValid = false;
            auditLog.ProcessingStatus = "rejected_signature";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }
        auditLog.SignatureValid = true;

        // 5. Idempotency Check
        var redisKey = $"webhook:prorouting:eventId:{eventId}";
        var hash = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawBody)));
        var idempotencyLock = await idempotencyService.AcquireLockAsync(redisKey, hash, TimeSpan.FromHours(24));
        
        if (!idempotencyLock)
        {
            auditLog.IsDuplicate = true;
            auditLog.ProcessingStatus = "rejected_duplicate";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            
            // For webhook duplicate replay, we return 200 OK so they stop retrying.
            return Results.Ok(new { message = "Duplicate event, ignored" }); 
        }

        // Deserialize safely
        var payload = System.Text.Json.JsonSerializer.Deserialize<ProRoutingWebhookPayload>(
            rawBody, 
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (payload == null)
        {
            auditLog.ProcessingStatus = "failed";
            auditLog.ErrorMessage = "Deserialization failed";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.BadRequest("Invalid payload");
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
                
            auditLog.ProcessingStatus = "processed_not_found";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            
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
                        
                        auditLog.ProcessingStatus = "processed_fallback_triggered";
                        auditDb.WebhookAuditLogs.Add(auditLog);
                        await auditDb.SaveChangesAsync(ct);
                        
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

            auditLog.ProcessingStatus = "accepted";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Webhook processed" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                "Invalid state transition for delivery {DeliveryId}: {Error}",
                delivery.Id, ex.Message);

            auditLog.ProcessingStatus = "processed_ignored_state";
            auditLog.ErrorMessage = ex.Message;
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);

            return Results.Ok(new { message = "State transition ignored", error = ex.Message });
        }
    }

    private static bool VerifyHmac(string payload, string providedSignature, string secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(providedSignature)) return false;

        var secretBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

        using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
        var computedHash = hmac.ComputeHash(payloadBytes);
        var computedHashHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(providedSignature);
        }
        catch
        {
            // If the provided signature is not a valid hex string, it fails immediately
            return false;
        }

        if (providedBytes.Length != computedHash.Length) return false;

        // Constant time comparison
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(computedHash, providedBytes);
    }
}