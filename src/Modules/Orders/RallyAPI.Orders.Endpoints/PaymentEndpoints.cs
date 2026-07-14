// File: src/Modules/Orders/RallyAPI.Orders.Endpoints/PaymentEndpoints.cs
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

            // 2. Time Drift logic
            // Extract `addedon` via precise IST parsing and convert to UTC.
            var addedOn = formData.GetValueOrDefault("addedon", "");
            if (string.IsNullOrWhiteSpace(addedOn))
            {
                auditLog.ProcessingStatus = "rejected_timestamp_missing";
                auditDb.WebhookAuditLogs.Add(auditLog);
                await auditDb.SaveChangesAsync();
                return Results.Ok(); // PayU requires OK to stop retries
            }

            try 
            {
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var istTime = DateTime.ParseExact(addedOn, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(istTime, istZone);
                
                // 10-minute time drift checking vs UTC.
                var toleranceSecs = config.GetValue<int>("WEBHOOK_PAYU_TIMESTAMP_TOLERANCE_SECONDS", 600);
                if (Math.Abs((DateTime.UtcNow - utcTime).TotalSeconds) > toleranceSecs)
                {
                    auditLog.ProcessingStatus = "rejected_timestamp_drift";
                    auditLog.TimestampValid = false;
                    auditDb.WebhookAuditLogs.Add(auditLog);
                    await auditDb.SaveChangesAsync();
                    return Results.Ok();
                }
                auditLog.TimestampValid = true;
            }
            catch
            {
                auditLog.ProcessingStatus = "rejected_timestamp_invalid";
                auditLog.TimestampValid = false;
                auditDb.WebhookAuditLogs.Add(auditLog);
                await auditDb.SaveChangesAsync();
                return Results.Ok();
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
            // Note: signature verification happens inside the handler 
            var result = await sender.Send(new ProcessPayuWebhookCommand(formData));

            if (result.IsSuccess)
            {
                auditLog.ProcessingStatus = "accepted";
            }
            else
            {
                auditLog.ProcessingStatus = "failed";
                auditLog.ErrorMessage = result.Error.Message;
            }
            
            // HMAC is verified inside handler, assuming valid if it reaches here and succeeds
            if (result.Error.Code == "Payment.InvalidHash") 
            {
                auditLog.SignatureValid = false;
                auditLog.ProcessingStatus = "rejected_signature";
            }
            else
            {
                auditLog.SignatureValid = true;
            }

            auditDb.WebhookAuditLogs.Add(auditLog);
            await auditDb.SaveChangesAsync();

            // Always return 200 to PayU — even on failure, to prevent retries
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

        // 5. Success/Failure return URLs (PayU redirects the BROWSER here via POST).
        //
        // PayU posts the result form (txnid, status, hash, …) to these endpoints — a static
        // SPA cannot read that POST body, so the browser must land on the backend first. We
        // resolve the order id from the txnid and 302-redirect (GET) to the SPA page, which
        // then confirms status via GET /api/payments/verify. The S2S /webhook remains the
        // source of truth for actually marking the order paid — this redirect is UX only.
        group.MapPost("/return/success", (HttpContext ctx,
            RallyAPI.Orders.Domain.Repositories.IPaymentRepository payments,
            Microsoft.Extensions.Options.IOptions<RallyAPI.Orders.Infrastructure.Services.PayU.PayUOptions> payuOptions,
            CancellationToken ct) =>
            BuildReturnRedirect(ctx, payments, payuOptions.Value.FrontendSuccessUrl, ct))
        .AllowAnonymous()
        .WithName("PaymentReturnSuccess");

        group.MapPost("/return/failure", (HttpContext ctx,
            RallyAPI.Orders.Domain.Repositories.IPaymentRepository payments,
            Microsoft.Extensions.Options.IOptions<RallyAPI.Orders.Infrastructure.Services.PayU.PayUOptions> payuOptions,
            CancellationToken ct) =>
            BuildReturnRedirect(ctx, payments, payuOptions.Value.FrontendFailureUrl, ct))
        .AllowAnonymous()
        .WithName("PaymentReturnFailure");


        return app;
    }

    /// <summary>
    /// Reads PayU's POSTed return form, maps txnid → order id, and 302-redirects the browser
    /// to the given SPA page with ?orderId=… appended. Falls back to the bare page (or "/") when
    /// the txnid can't be resolved, so the user is never stranded on the API host.
    /// </summary>
    private static async Task<IResult> BuildReturnRedirect(
        HttpContext ctx,
        RallyAPI.Orders.Domain.Repositories.IPaymentRepository payments,
        string frontendUrl,
        CancellationToken ct)
    {
        // Never crash the browser redirect on a missing/oversized form — worst case we send
        // the user to the bare success/failure page without an id.
        string txnId = string.Empty;
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync(ct);
            txnId = form["txnid"].ToString();
        }

        var target = string.IsNullOrWhiteSpace(frontendUrl) ? "/" : frontendUrl;

        if (!string.IsNullOrWhiteSpace(txnId))
        {
            var payment = await payments.GetByTxnIdAsync(txnId, ct);
            if (payment is not null)
            {
                var separator = target.Contains('?') ? '&' : '?';
                target = $"{target}{separator}orderId={payment.OrderId}";
            }
        }

        return Results.Redirect(target);
    }
}

// === Request DTOs ===

public record InitiatePaymentRequest(Guid OrderId);
public record VerifyPaymentRequest(string TxnId);
public record RefundPaymentRequest(Guid OrderId, decimal? Amount);