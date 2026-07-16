// File: src/Modules/Orders/RallyAPI.Orders.Endpoints/PaymentEndpoints.cs
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Commands.InitiatePayment;
using RallyAPI.Orders.Application.Commands.ProcessPayuWebhook;
using RallyAPI.Orders.Application.Commands.RefundPayment;
using RallyAPI.Orders.Application.Commands.VerifyPayment;

namespace RallyAPI.Orders.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("Payments");

        // 1. Initiate payment — returns PayU checkout params
        group.MapPost("/initiate", async (
            InitiatePaymentRequest request,
            RallyAPI.SharedKernel.Abstractions.ICurrentUserService currentUser,
            ISender sender) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new InitiatePaymentCommand(
                request.OrderId,
                currentUser.UserId.Value));

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error.Message });
        })
        .RequireAuthorization("Customer")
        .RequireRateLimiting("login")
        .WithName("InitiatePayment");
        // 2. PayU webhook (S2S callback) — source of truth
        group.MapPost("/webhook", async (
            HttpContext httpContext,
            Microsoft.Extensions.Configuration.IConfiguration config,
            RallyAPI.SharedKernel.Infrastructure.RedisIdempotencyService idempotencyService,
            RallyAPI.Infrastructure.Persistence.AuditDbContext auditDb,
            ISender sender) =>
        {
            var auditLog = new RallyAPI.SharedKernel.Domain.Entities.WebhookAuditLog
            {
                Id = Guid.NewGuid(),
                Source = "payu",
                ReceivedAt = DateTimeOffset.UtcNow,
                SourceIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                CorrelationId = Guid.NewGuid()
            };

            // PayU sends form-urlencoded POST
            var form = await httpContext.Request.ReadFormAsync();
            var formData = form.ToDictionary(
                x => x.Key,
                x => x.Value.ToString());

            // 1. Audit logic
            var rawBody = string.Join("&", formData.Select(kv => $"{kv.Key}={System.Uri.EscapeDataString(kv.Value)}"));
            auditLog.RawBody = rawBody; // in real systems we'd encrypt
            
            var txnId = formData.GetValueOrDefault("txnid", "");
            var status = formData.GetValueOrDefault("status", "unknown");
            var mihpayid = formData.GetValueOrDefault("mihpayid", "");
            
            auditLog.EventId = $"payu:{txnId}:{mihpayid}:{status}";

            // 2. Timestamp check — FOR AUDIT ONLY, never rejects.
            // `addedon` is PayU's transaction-creation time, NOT the webhook send
            // time. PayU retries a failed/undelivered webhook for hours/days and
            // every retry carries the ORIGINAL `addedon`, so a drift check here
            // would silently discard legitimate, successful payments (and leave the
            // order stuck in Pending). Payment authenticity is guaranteed by the
            // reverse-hash verification inside the handler — that is the real
            // security control, not this timestamp.
            var addedOn = formData.GetValueOrDefault("addedon", "");
            try
            {
                if (!string.IsNullOrWhiteSpace(addedOn))
                {
                    var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    var istTime = DateTime.ParseExact(addedOn, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                    var utcTime = TimeZoneInfo.ConvertTimeToUtc(istTime, istZone);

                    var toleranceSecs = config.GetValue<int>("WEBHOOK_PAYU_TIMESTAMP_TOLERANCE_SECONDS", 600);
                    auditLog.TimestampValid = Math.Abs((DateTime.UtcNow - utcTime).TotalSeconds) <= toleranceSecs;
                }
                else
                {
                    auditLog.TimestampValid = false;
                }
            }
            catch
            {
                auditLog.TimestampValid = false;
            }

            // 3. Idempotency Check using Redis
            var redisKey = $"webhook:payu:eventId:{auditLog.EventId}";
            var hash = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawBody)));
            var idempotencyLock = await idempotencyService.AcquireLockAsync(redisKey, hash, TimeSpan.FromHours(24));

            if (!idempotencyLock)
            {
                auditLog.ProcessingStatus = "rejected_duplicate";
                auditLog.IsDuplicate = true;
                auditDb.WebhookAuditLogs.Add(auditLog);
                await auditDb.SaveChangesAsync();
                return Results.Ok(); 
            }

            // 4. Send to MediatR
            // Note: signature verification happens inside the handler.
            var result = await sender.Send(new ProcessPayuWebhookCommand(formData));

            if (result.IsSuccess)
            {
                auditLog.ProcessingStatus = "accepted";
                auditLog.SignatureValid = true;
            }
            else
            {
                auditLog.ErrorMessage = result.Error.Message;

                if (result.Error.Code == "Payment.InvalidHash")
                {
                    // Genuinely bad signature — reject and keep the lock (real duplicate/forgery).
                    auditLog.SignatureValid = false;
                    auditLog.ProcessingStatus = "rejected_signature";
                }
                else
                {
                    // Transient/processing failure (DB blip, etc.). Release the Redis
                    // lock so PayU's next retry is NOT rejected as a duplicate and can
                    // actually be reprocessed — otherwise the payment would be lost for
                    // the full 24h lock TTL and the order would stay stuck in Pending.
                    auditLog.SignatureValid = true;
                    auditLog.ProcessingStatus = "failed";
                    await idempotencyService.ReleaseLockAsync(redisKey);
                }
            }

            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync();

            // Always return 200 to PayU — even on failure, to prevent retries
            // hammering us; genuine retries will still reprocess because the lock
            // was released above.
            return Results.Ok();
        })
        .AllowAnonymous()  // PayU server-to-server, no JWT
        .WithName("PayUWebhook");

        // 3. Verify payment — frontend backup check
        group.MapPost("/verify", async (
            VerifyPaymentRequest request,
            RallyAPI.SharedKernel.Abstractions.ICurrentUserService currentUser,
            ISender sender) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new VerifyPaymentCommand(
                request.TxnId,
                currentUser.UserId.Value));

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error.Message });
        })
        .RequireAuthorization("Customer")
        .WithName("VerifyPayment");

        // 4. Refund — admin only
        group.MapPost("/refund", async (
            RefundPaymentRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new RefundPaymentCommand(
                request.OrderId,
                request.Amount));

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error.Message });
        })
        .RequireAuthorization("Admin")
        .WithName("RefundPayment");

        // 5. Success/Failure return URLs (PayU POSTs the full signed result here
        //    via the customer's browser on EVERY transaction — surl/furl).
        //    This is the primary, always-delivered confirmation path. We process
        //    the payment here (hash-verified, idempotent) BEFORE redirecting the
        //    browser to the frontend, so an order can never be left in Pending
        //    just because the S2S webhook is not configured or the frontend
        //    forgot to call /verify.
        group.MapPost("/return/success", async (
            HttpContext httpContext,
            IConfiguration config,
            ISender sender) =>
        {
            var formData = await ReadPayuFormAsync(httpContext);
            var txnId = formData.GetValueOrDefault("txnid", "");

            // Same handler as the webhook: verifies hash, marks payment Paid,
            // transitions order Pending → Paid. Safe to run multiple times.
            var result = await sender.Send(new ProcessPayuWebhookCommand(formData));
            if (!result.IsSuccess)
            {
                httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("PaymentReturn")
                    .LogError("PayU return/success processing failed for TxnId {TxnId}: {Error}",
                        txnId, result.Error.Message);
            }

            return Results.Redirect(BuildFrontendRedirect(config, "PayU:FrontendSuccessUrl", "/payment-success", txnId));
        })
        .AllowAnonymous()
        .WithName("PaymentReturnSuccess");

        group.MapPost("/return/failure", async (
            HttpContext httpContext,
            IConfiguration config,
            ISender sender) =>
        {
            var formData = await ReadPayuFormAsync(httpContext);
            var txnId = formData.GetValueOrDefault("txnid", "");

            // Record the failure/cancellation so the payment isn't left dangling.
            await sender.Send(new ProcessPayuWebhookCommand(formData));

            return Results.Redirect(BuildFrontendRedirect(config, "PayU:FrontendFailureUrl", "/payment-failed", txnId));
        })
        .AllowAnonymous()
        .WithName("PaymentReturnFailure");


        return app;
    }

    /// <summary>Reads PayU's form-urlencoded POST body into a dictionary.</summary>
    private static async Task<Dictionary<string, string>> ReadPayuFormAsync(HttpContext httpContext)
    {
        var form = await httpContext.Request.ReadFormAsync();
        return form.ToDictionary(x => x.Key, x => x.Value.ToString());
    }

    /// <summary>
    /// Builds the browser redirect target after processing a PayU return POST.
    /// Uses the configured frontend URL when present (append txnid so the SPA can
    /// look up / verify the order), otherwise falls back to a relative path.
    /// </summary>
    private static string BuildFrontendRedirect(
        IConfiguration config, string configKey, string fallback, string txnId)
    {
        var baseUrl = config[configKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return fallback;

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return string.IsNullOrEmpty(txnId)
            ? baseUrl
            : $"{baseUrl}{separator}txnid={Uri.EscapeDataString(txnId)}";
    }
}

// === Request DTOs ===

public record InitiatePaymentRequest(Guid OrderId);
public record VerifyPaymentRequest(string TxnId);
public record RefundPaymentRequest(Guid OrderId, decimal? Amount);