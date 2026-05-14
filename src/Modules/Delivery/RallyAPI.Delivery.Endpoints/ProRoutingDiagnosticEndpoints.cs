using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Integrations.ProRouting;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Delivery.Endpoints;

/// <summary>
/// Dev-only diagnostic endpoints to verify ProRouting integration end-to-end
/// without going through the full order/payment flow.
///
/// Register only when env is Development.
/// </summary>
public static class ProRoutingDiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapProRoutingDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diag/prorouting")
            .WithTags("Diagnostics: ProRouting")
            .WithOpenApi();

        group.MapGet("/config", ShowConfig)
            .WithName("DiagProRoutingConfig")
            .WithSummary("Show ProRouting config (key redacted)");

        group.MapPost("/create-task", CreateTask)
            .WithName("DiagProRoutingCreateTask")
            .WithSummary("Fire a real CreateTaskAsync call with sample payload");

        group.MapGet("/status/{taskId}", GetStatus)
            .WithName("DiagProRoutingTaskStatus")
            .WithSummary("Poll ProRouting for current task state");

        group.MapPost("/cancel/{taskId}", CancelTask)
            .WithName("DiagProRoutingCancelTask")
            .WithSummary("Cancel a ProRouting task (to clean up after a test)");

        return app;
    }

    private static IResult ShowConfig(
        IOptions<ProRoutingOptions> prOptions,
        IOptions<DispatchOptions> dispatchOptions)
    {
        var pr = prOptions.Value;
        var ds = dispatchOptions.Value;

        var keyHint = string.IsNullOrWhiteSpace(pr.ApiKey)
            ? "<MISSING>"
            : pr.ApiKey.Length <= 8
                ? "<short, likely placeholder>"
                : $"{pr.ApiKey[..4]}…{pr.ApiKey[^4..]} (len={pr.ApiKey.Length})";

        return Results.Ok(new
        {
            ProRouting = new
            {
                pr.BaseUrl,
                ApiKeyHint = keyHint,
                pr.TimeoutSeconds,
                pr.DefaultOrderCategory,
                pr.DefaultSearchCategory,
                pr.Enabled
            },
            Dispatch = new
            {
                ds.WebhookUrl,
                ds.AcceptanceTimeoutSeconds,
                ds.SearchRadiusKm,
                ds.MaxRidersToTry,
                WebhookUrlLooksReal = !string.IsNullOrWhiteSpace(ds.WebhookUrl)
                    && !ds.WebhookUrl.Contains("your-domain.com", StringComparison.OrdinalIgnoreCase)
                    && !ds.WebhookUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            }
        });
    }

    private static async Task<IResult> CreateTask(
        [FromBody] DiagCreateTaskRequest? overrides,
        IThirdPartyDeliveryProvider provider,
        IOptions<DispatchOptions> dispatchOptions,
        CancellationToken ct)
    {
        var callbackUrl = !string.IsNullOrWhiteSpace(overrides?.CallbackUrl)
            ? overrides!.CallbackUrl!
            : dispatchOptions.Value.WebhookUrl;

        var orderNumber = overrides?.OrderNumber ?? $"DIAG-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var req = new CreateTaskRequest
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = orderNumber,
            DeliveryRequestId = Guid.NewGuid(),

            // Pickup — Koramangala, Bangalore (sample restaurant)
            PickupLatitude = overrides?.PickupLatitude ?? 12.9352,
            PickupLongitude = overrides?.PickupLongitude ?? 77.6245,
            PickupPincode = overrides?.PickupPincode ?? "560034",
            PickupAddressLine1 = "Diagnostic Restaurant, 100 Feet Rd",
            PickupCity = "Bengaluru",
            PickupState = "Karnataka",
            PickupContactName = "Diag Restaurant",
            PickupContactPhone = "9999900001",
            StoreId = "DIAG-STORE",

            // Drop — Indiranagar, Bangalore (sample customer)
            DropLatitude = overrides?.DropLatitude ?? 12.9784,
            DropLongitude = overrides?.DropLongitude ?? 77.6408,
            DropPincode = overrides?.DropPincode ?? "560038",
            DropAddressLine1 = "Diagnostic Customer, 12th Main",
            DropCity = "Bengaluru",
            DropState = "Karnataka",
            DropContactName = "Diag Customer",
            DropContactPhone = "9999900002",

            OrderAmount = overrides?.OrderAmount ?? 500m,
            OrderCategory = overrides?.OrderCategory ?? "F&B",
            PickupCode = "123456",
            DropCode = "9999",
            IsOrderReady = true,
            SelectionMode = "fastest_agent",
            CallbackUrl = callbackUrl,
            OrderItems = new[]
            {
                new TaskOrderItem("Butter Chicken", 1, 280m),
                new TaskOrderItem("Garlic Naan", 2, 60m)
            }
        };

        var startedAt = DateTime.UtcNow;
        var result = await provider.CreateTaskAsync(req, ct);
        var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

        return Results.Ok(new
        {
            Sent = new
            {
                req.OrderNumber,
                req.OrderId,
                req.DeliveryRequestId,
                Pickup = new { req.PickupLatitude, req.PickupLongitude, req.PickupPincode },
                Drop = new { req.DropLatitude, req.DropLongitude, req.DropPincode },
                req.CallbackUrl,
                req.OrderAmount
            },
            ElapsedMs = (int)elapsedMs,
            Result = new
            {
                result.IsSuccess,
                result.TaskId,
                result.ClientOrderId,
                result.State,
                result.ErrorMessage,
                result.ProviderName
            },
            NextStep = result.IsSuccess
                ? $"GET /api/diag/prorouting/status/{result.TaskId} to poll, or wait for webhook to {callbackUrl}"
                : "Inspect ErrorMessage. Check serilog logs for the raw HTTP body returned by ProRouting."
        });
    }

    private static async Task<IResult> GetStatus(
        string taskId,
        IThirdPartyDeliveryProvider provider,
        CancellationToken ct)
    {
        var result = await provider.GetTaskStatusAsync(taskId, ct);
        return Results.Ok(new
        {
            result.IsSuccess,
            result.TaskId,
            result.State,
            result.RiderName,
            result.RiderPhone,
            result.TrackingUrl,
            result.ErrorMessage,
            result.UpdatedAt
        });
    }

    private static async Task<IResult> CancelTask(
        string taskId,
        [FromQuery] string? reason,
        IThirdPartyDeliveryProvider provider,
        CancellationToken ct)
    {
        var result = await provider.CancelTaskAsync(taskId, reason ?? "Diagnostic cleanup", ct);
        return Results.Ok(new { result.IsSuccess, result.ErrorMessage });
    }
}

public sealed record DiagCreateTaskRequest
{
    public string? OrderNumber { get; init; }
    public string? CallbackUrl { get; init; }
    public double? PickupLatitude { get; init; }
    public double? PickupLongitude { get; init; }
    public string? PickupPincode { get; init; }
    public double? DropLatitude { get; init; }
    public double? DropLongitude { get; init; }
    public string? DropPincode { get; init; }
    public decimal? OrderAmount { get; init; }
    public string? OrderCategory { get; init; }
}
