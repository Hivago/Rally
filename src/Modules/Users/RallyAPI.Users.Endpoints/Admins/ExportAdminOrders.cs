using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.SharedKernel.Abstractions.Orders;
using RallyAPI.Users.Application.Abstractions;
using System.Security.Claims;

namespace RallyAPI.Users.Endpoints.Admins;

/// <summary>
/// Streams matching orders as CSV. Bypasses pagination; intended for spreadsheet exports.
/// Per-admin rate limited to 5/min in production via the "admin-export" policy.
/// </summary>
public class ExportAdminOrders : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/orders/export", HandleAsync)
            .WithName("ExportAdminOrders")
            .WithTags("Admins")
            .WithSummary("Stream matching orders as CSV (admin panel)")
            .RequireAuthorization("Admin")
            .RequireRateLimiting("admin-export");
    }

    private static async Task HandleAsync(
        HttpContext httpContext,
        ClaimsPrincipal user,
        IAdminOrderQueryService orderService,
        IAdminRepository adminRepository,
        CancellationToken cancellationToken,
        string? status = "all",
        string? search = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var adminId = Guid.Parse(user.FindFirstValue("sub")!);

        // Verify the admin exists and is active. We do this here instead of via a MediatR
        // handler because streaming responses don't fit the Result<T> pipeline cleanly.
        var admin = await adminRepository.GetByIdAsync(adminId, cancellationToken);
        if (admin is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = "Admin.NotFound", message = "Admin not found." },
                cancellationToken);
            return;
        }

        var tab = ParseTab(status);
        var filter = new AdminOrdersFilter(tab, search, from, to, Page: 1, PageSize: 100);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        httpContext.Response.ContentType = "text/csv; charset=utf-8";
        httpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"orders-{stamp}.csv\"";

        await using var writer = new StreamWriter(
            httpContext.Response.Body,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(
            "OrderNumber,CreatedAtUtc,Status,PaymentStatus,FulfillmentType,CustomerName,CustomerPhone,RestaurantName,RiderName,ItemCount,Total,Currency,IsEscalated,CancellationReason");

        await foreach (var row in orderService.StreamForExportAsync(filter, cancellationToken))
        {
            await writer.WriteLineAsync(FormatRow(row));
        }
    }

    private static AdminOrdersTab ParseTab(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "active" => AdminOrdersTab.Active,
            "escalated" => AdminOrdersTab.Escalated,
            "failed" => AdminOrdersTab.Failed,
            _ => AdminOrdersTab.All
        };

    private static string FormatRow(AdminOrderExportRow row) =>
        string.Join(",",
            Csv(row.OrderNumber),
            row.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
            Csv(row.Status),
            Csv(row.PaymentStatus),
            Csv(row.FulfillmentType),
            Csv(row.CustomerName),
            Csv(row.CustomerPhone),
            Csv(row.RestaurantName),
            Csv(row.RiderName),
            row.ItemCount.ToString(CultureInfo.InvariantCulture),
            row.Total.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(row.Currency),
            row.IsEscalated ? "true" : "false",
            Csv(row.CancellationReason));

    /// <summary>
    /// CSV-escape a value: wrap in quotes if it contains comma, quote, CR, or LF;
    /// double up any embedded quotes.
    /// </summary>
    private static string Csv(string? value)
    {
        if (value is null) return string.Empty;
        var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
