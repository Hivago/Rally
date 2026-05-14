using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RallyAPI.Delivery.Application.Commands.TriggerDispatch;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.Integrations.ProRouting.Models;

namespace RallyAPI.Delivery.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Webhooks")
            .WithOpenApi();

        // ProRouting Status Callback (state transitions: Agent-assigned, Order-picked-up, RTO-Initiated, ...)
        group.MapPost("/prorouting", HandleProRoutingStatusCallback)
            .WithName("ProRoutingStatusCallback")
            .WithSummary("Handle ProRouting status update callbacks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // Alias path so we can document both /prorouting and /prorouting/status with ProRouting.
        group.MapPost("/prorouting/status", HandleProRoutingStatusCallback)
            .WithName("ProRoutingStatusCallbackAlias")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // ProRouting Track Callback (bulk live GPS for active orders)
        group.MapPost("/prorouting/track", HandleProRoutingTrackCallback)
            .WithName("ProRoutingTrackCallback")
            .WithSummary("Handle ProRouting bulk live-location callbacks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    // ============================================================
    // STATUS CALLBACK
    // ============================================================
    private static async Task<IResult> HandleProRoutingStatusCallback(
        HttpRequest request,
        IConfiguration config,
        DeliveryDbContext dbContext,
        RallyAPI.Infrastructure.Persistence.AuditDbContext auditDb,
        RallyAPI.SharedKernel.Infrastructure.RedisIdempotencyService idempotencyService,
        ISender sender,
        ILogger<ProRoutingStatusCallback> logger,
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

        var rawBody = await ReadBodyAsync(request, ct);
        auditLog.RawBody = rawBody;

        var (authOk, authReason, eventId) = await AuthenticateAsync(request, rawBody, config, auditLog);
        if (!authOk)
        {
            auditLog.ProcessingStatus = authReason;
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }
        auditLog.EventId = eventId;

        // Idempotency
        var idempotencyKey = $"webhook:prorouting:status:{eventId}";
        var bodyHash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        if (!await idempotencyService.AcquireLockAsync(idempotencyKey, bodyHash, TimeSpan.FromHours(24)))
        {
            auditLog.IsDuplicate = true;
            auditLog.ProcessingStatus = "rejected_duplicate";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Duplicate event, ignored" });
        }

        ProRoutingStatusCallback? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<ProRoutingStatusCallback>(
                rawBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException ex)
        {
            auditLog.ProcessingStatus = "failed";
            auditLog.ErrorMessage = "Deserialization failed: " + ex.Message;
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.BadRequest("Invalid payload");
        }

        if (payload?.Order is null || string.IsNullOrEmpty(payload.Order.Id))
        {
            auditLog.ProcessingStatus = "failed";
            auditLog.ErrorMessage = "Missing order.id in payload";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.BadRequest("Invalid payload");
        }

        var order = payload.Order;
        logger.LogInformation(
            "ProRouting status callback: OrderId={OrderId}, State={State}",
            order.Id, order.State);

        var delivery = await FindDeliveryAsync(dbContext, order.Id, order.ClientOrderId, ct);
        if (delivery is null)
        {
            logger.LogWarning("Delivery not found for ProRouting status callback: {OrderId}", order.Id);
            auditLog.ProcessingStatus = "processed_not_found";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Delivery not found, ignored" });
        }

        try
        {
            var stateNormalized = order.State?.Trim().ToLowerInvariant() ?? "";

            switch (stateNormalized)
            {
                case "agent-assigned":
                case "agent_assigned":
                    var trackingUrl = order.TrackingUrlAlt ?? order.TrackingUrl;
                    delivery.RecordProviderCost(
                        lspFee: order.Fees?.Lsp,
                        platformFee: order.Fees?.Platform,
                        totalWithTax: order.Fees?.TotalWithTax,
                        distanceKm: order.Distance,
                        networkOrderId: order.NetworkOrderId);
                    delivery.Update3PLRiderInfo(
                        order.Rider?.Name,
                        order.Rider?.Phone,
                        trackingUrl);
                    if (delivery.Status == DeliveryRequestStatus.Searching3PL)
                    {
                        delivery.Assign3PLRider(
                            order.Id,
                            order.LogisticsSeller ?? "ProRouting",
                            order.Rider?.Name,
                            order.Rider?.Phone,
                            trackingUrl,
                            order.Price ?? delivery.QuotedPrice);
                    }
                    break;

                case "at-pickup":
                case "at_pickup":
                    if (delivery.Status is DeliveryRequestStatus.RiderAssigned
                        or DeliveryRequestStatus.Assigned3PL
                        or DeliveryRequestStatus.RiderEnRoutePickup)
                    {
                        if (delivery.Status != DeliveryRequestStatus.RiderEnRoutePickup)
                            delivery.MarkRiderEnRoutePickup();
                        delivery.MarkRiderArrivedPickup();
                    }
                    break;

                case "picked-up":
                case "picked_up":
                case "order-picked-up":
                    delivery.MarkPickedUp();
                    break;

                case "at-delivery":
                case "at_delivery":
                    if (delivery.Status == DeliveryRequestStatus.PickedUp)
                        delivery.MarkRiderEnRouteDrop();
                    delivery.MarkRiderArrivedDrop();
                    break;

                case "delivered":
                case "order-delivered":
                    delivery.MarkDelivered();
                    break;

                case "rto-initiated":
                case "rto_initiated":
                    // If we never picked up (3PL failed before pickup), fall back to own fleet.
                    if (delivery.Status is DeliveryRequestStatus.Searching3PL or DeliveryRequestStatus.Assigned3PL)
                    {
                        logger.LogInformation(
                            "3PL failed pre-pickup for delivery {DeliveryId}. Falling back to own fleet.",
                            delivery.Id);
                        delivery.TransitionToOwnFleetSearch();
                        await dbContext.SaveChangesAsync(ct);
                        _ = sender.Send(new TriggerDispatchCommand { DeliveryRequestId = delivery.Id }, ct);

                        auditLog.ProcessingStatus = "processed_fallback_triggered";
                        auditDb.WebhookAuditLogs.Add(auditLog);
                        await auditDb.SaveChangesAsync(ct);
                        return Results.Ok(new { message = "Fallback to own fleet triggered" });
                    }
                    delivery.InitiateRto(order.CancelReason);
                    break;

                case "rto-delivered":
                case "rto_delivered":
                    delivery.MarkRtoDelivered();
                    break;

                case "rto-disposed":
                case "rto_disposed":
                    delivery.MarkRtoDisposed();
                    break;

                case "cancelled":
                case "failed":
                    if (delivery.Status is DeliveryRequestStatus.Searching3PL or DeliveryRequestStatus.Assigned3PL)
                    {
                        delivery.TransitionToOwnFleetSearch();
                        await dbContext.SaveChangesAsync(ct);
                        _ = sender.Send(new TriggerDispatchCommand { DeliveryRequestId = delivery.Id }, ct);

                        auditLog.ProcessingStatus = "processed_fallback_triggered";
                        auditDb.WebhookAuditLogs.Add(auditLog);
                        await auditDb.SaveChangesAsync(ct);
                        return Results.Ok(new { message = "Fallback to own fleet triggered" });
                    }
                    delivery.MarkFailed(
                        DeliveryFailureReason.ThirdPartyFailed,
                        order.CancelReason ?? order.State);
                    break;

                default:
                    logger.LogDebug("Unhandled ProRouting state: {State}", order.State);
                    break;
            }

            await dbContext.SaveChangesAsync(ct);
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

    // ============================================================
    // TRACK CALLBACK (bulk live GPS)
    // ============================================================
    private static async Task<IResult> HandleProRoutingTrackCallback(
        HttpRequest request,
        IConfiguration config,
        DeliveryDbContext dbContext,
        RallyAPI.Infrastructure.Persistence.AuditDbContext auditDb,
        ILogger<ProRoutingTrackCallback> logger,
        CancellationToken ct)
    {
        var sourceIp = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var auditLog = new RallyAPI.SharedKernel.Domain.Entities.WebhookAuditLog
        {
            Id = Guid.NewGuid(),
            Source = "prorouting_track",
            ReceivedAt = DateTimeOffset.UtcNow,
            SourceIp = sourceIp,
            CorrelationId = Guid.NewGuid()
        };

        var rawBody = await ReadBodyAsync(request, ct);
        auditLog.RawBody = rawBody;

        var (authOk, authReason, _) = await AuthenticateAsync(request, rawBody, config, auditLog);
        if (!authOk)
        {
            auditLog.ProcessingStatus = authReason;
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }

        // Track callbacks are continuous + bulk — no idempotency lock; we
        // reconcile by ignoring stale updates inside DeliveryRequest.UpdateLiveLocation.

        ProRoutingTrackCallback? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<ProRoutingTrackCallback>(
                rawBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException ex)
        {
            auditLog.ProcessingStatus = "failed";
            auditLog.ErrorMessage = ex.Message;
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.BadRequest("Invalid payload");
        }

        if (payload is null || payload.Orders.Count == 0)
        {
            auditLog.ProcessingStatus = "accepted_empty";
            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync(ct);
            return Results.Ok(new { message = "No orders in payload" });
        }

        var taskIds = payload.Orders.Select(o => o.Id).ToList();
        var deliveries = await dbContext.DeliveryRequests
            .Where(r => r.ExternalTaskId != null && taskIds.Contains(r.ExternalTaskId))
            .ToListAsync(ct);

        var deliveryByTaskId = deliveries.ToDictionary(r => r.ExternalTaskId!, r => r);

        var updated = 0;
        foreach (var trackOrder in payload.Orders)
        {
            if (!deliveryByTaskId.TryGetValue(trackOrder.Id, out var delivery))
                continue;

            var loc = trackOrder.Rider?.LastLocation;
            if (loc is null)
                continue;

            var updatedAt = ParseProRoutingTimestamp(loc.UpdatedAt) ?? DateTime.UtcNow;
            delivery.UpdateLiveLocation(loc.Lat, loc.Lng, updatedAt);

            // Opportunistically refresh rider name/phone if changed (LSP can reassign).
            if (!string.IsNullOrEmpty(trackOrder.Rider?.Name) || !string.IsNullOrEmpty(trackOrder.Rider?.Phone))
            {
                delivery.Update3PLRiderInfo(
                    trackOrder.Rider?.Name,
                    trackOrder.Rider?.Phone,
                    trackOrder.TrackingUrl);
            }

            updated++;
        }

        await dbContext.SaveChangesAsync(ct);
        auditLog.ProcessingStatus = $"accepted_updated_{updated}_of_{payload.Orders.Count}";
        auditDb.WebhookAuditLogs.Add(auditLog);
        await auditDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "ProRouting track callback processed: {Updated}/{Total} deliveries updated",
            updated, payload.Orders.Count);

        return Results.Ok(new { message = "Track processed", updated, total = payload.Orders.Count });
    }

    // ============================================================
    // AUTH (Option B: HMAC if signature header present, else x-pro-api-key)
    // ============================================================
    private static async Task<(bool ok, string reason, string eventId)> AuthenticateAsync(
        HttpRequest request,
        string rawBody,
        IConfiguration config,
        RallyAPI.SharedKernel.Domain.Entities.WebhookAuditLog auditLog)
    {
        var hasHmacHeaders = request.Headers.ContainsKey("X-ProRouting-Signature")
            && request.Headers.ContainsKey("X-ProRouting-Timestamp");

        if (hasHmacHeaders)
        {
            return await AuthenticateHmacAsync(request, rawBody, config, auditLog);
        }

        // Fallback: x-pro-api-key (matches actual ProRouting contract)
        var providedKey = request.Headers["x-pro-api-key"].ToString();
        var expectedKey = config.GetValue<string>("PROROUTING_INBOUND_API_KEY")
                         ?? config.GetValue<string>("ProRouting:ApiKey")
                         ?? string.Empty;

        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(providedKey))
        {
            // ProRouting preprod sends callbacks unsigned. Allow in Development so the round-trip
            // can be tested locally via ngrok. Production envs still reject unsigned callbacks.
            var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                var devEventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)))[..32];
                return (true, "accepted_dev_unsigned", devEventId);
            }

            return (false, "rejected_missing_api_key", string.Empty);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedKey),
                Encoding.UTF8.GetBytes(expectedKey)))
        {
            return (false, "rejected_api_key_mismatch", string.Empty);
        }

        // Generate eventId from body hash since ProRouting doesn't send one with api-key auth
        var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)))[..32];
        return (true, "accepted", eventId);
    }

    private static Task<(bool ok, string reason, string eventId)> AuthenticateHmacAsync(
        HttpRequest request,
        string rawBody,
        IConfiguration config,
        RallyAPI.SharedKernel.Domain.Entities.WebhookAuditLog auditLog)
    {
        if (!request.Headers.TryGetValue("X-ProRouting-Signature", out var sigVals)
            || !request.Headers.TryGetValue("X-ProRouting-Timestamp", out var tsVals)
            || !request.Headers.TryGetValue("X-ProRouting-Event-Id", out var eventIdVals))
        {
            return Task.FromResult((false, "rejected_missing_headers", string.Empty));
        }

        var providedSignature = sigVals.ToString();
        var timestampStr = tsVals.ToString();
        var eventId = eventIdVals.ToString();

        if (!long.TryParse(timestampStr, out var timestampSecs))
            return Task.FromResult((false, "rejected_timestamp_invalid", eventId));

        var timestampDate = DateTimeOffset.FromUnixTimeSeconds(timestampSecs);
        var toleranceSecs = config.GetValue<int>("WEBHOOK_PROROUTING_TIMESTAMP_TOLERANCE_SECONDS", 300);
        if (Math.Abs((DateTimeOffset.UtcNow - timestampDate).TotalSeconds) > toleranceSecs)
            return Task.FromResult((false, "rejected_timestamp", eventId));

        auditLog.TimestampValid = true;

        var secretCurrent = config.GetValue<string>("WEBHOOK_PROROUTING_SECRET_CURRENT") ?? "";
        var secretPrevious = config.GetValue<string>("WEBHOOK_PROROUTING_SECRET_PREVIOUS") ?? "";
        var payloadToHash = timestampStr + "." + rawBody;

        if (!VerifyHmac(payloadToHash, providedSignature, secretCurrent)
            && !VerifyHmac(payloadToHash, providedSignature, secretPrevious))
        {
            auditLog.SignatureValid = false;
            return Task.FromResult((false, "rejected_signature", eventId));
        }

        auditLog.SignatureValid = true;
        return Task.FromResult((true, "accepted", eventId));
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0;
        return body;
    }

    private static async Task<RallyAPI.Delivery.Domain.Entities.DeliveryRequest?> FindDeliveryAsync(
        DeliveryDbContext dbContext,
        string externalTaskId,
        string? clientOrderId,
        CancellationToken ct)
    {
        var byTaskId = await dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.ExternalTaskId == externalTaskId, ct);
        if (byTaskId is not null)
            return byTaskId;

        if (string.IsNullOrEmpty(clientOrderId))
            return null;

        return await dbContext.DeliveryRequests
            .FirstOrDefaultAsync(r => r.OrderNumber == clientOrderId, ct);
    }

    private static DateTime? ParseProRoutingTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        // Per docs: "yyyy-MM-dd HH:mm:ss" in IST
        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var fallback)
            ? fallback
            : null;
    }

    private static bool VerifyHmac(string payload, string providedSignature, string secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(providedSignature))
            return false;

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(secretBytes);
        var computedHash = hmac.ComputeHash(payloadBytes);

        byte[] providedBytes;
        try { providedBytes = Convert.FromHexString(providedSignature); }
        catch { return false; }

        if (providedBytes.Length != computedHash.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(computedHash, providedBytes);
    }
}
