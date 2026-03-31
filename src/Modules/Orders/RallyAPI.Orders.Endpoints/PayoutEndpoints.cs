using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Orders.Application.Commands.ProcessPayout;
using RallyAPI.Orders.Application.DTOs;
using RallyAPI.Orders.Application.Queries.GetGstSummary;
using RallyAPI.Orders.Application.Queries.GetPayoutDetail;
using RallyAPI.Orders.Application.Queries.GetPayoutsByOwner;
using RallyAPI.Orders.Application.Queries.GetPendingPayouts;
using RallyAPI.Orders.Application.Queries.GetRestaurantEarnings;
using RallyAPI.Orders.Application.Queries.GetTdsSummary;

namespace RallyAPI.Orders.Endpoints;

public static class PayoutEndpoints
{
    public static IEndpointRouteBuilder MapPayoutEndpoints(this IEndpointRouteBuilder app)
    {
        // Restaurant owner endpoints
        var restaurantGroup = app.MapGroup("/api/restaurants/payouts")
            .WithTags("Restaurant Payouts")
            .WithOpenApi()
            .RequireAuthorization("Restaurant");

        restaurantGroup.MapGet("/earnings", GetEarnings)
            .WithName("GetRestaurantEarnings")
            .WithSummary("Get current week's earnings summary");

        restaurantGroup.MapGet("/", GetPayoutHistory)
            .WithName("GetPayoutHistory")
            .WithSummary("Get payout history for the restaurant owner");

        restaurantGroup.MapGet("/{payoutId:guid}", GetPayoutDetail)
            .WithName("GetPayoutDetail")
            .WithSummary("Get payout detail with order-level breakdown");

        restaurantGroup.MapGet("/gst-summary", GetGstSummary)
            .WithName("GetGstSummary")
            .WithSummary("Get GST summary for a date range");

        restaurantGroup.MapGet("/tds-summary", GetTdsSummary)
            .WithName("GetTdsSummary")
            .WithSummary("Get TDS summary for a date range");

        // Admin endpoints
        var adminGroup = app.MapGroup("/api/admin/payouts")
            .WithTags("Admin Payouts")
            .WithOpenApi()
            .RequireAuthorization("Admin");

        adminGroup.MapGet("/pending", GetPendingPayouts)
            .WithName("GetPendingPayouts")
            .WithSummary("Get all pending payouts waiting for processing");

        adminGroup.MapPut("/{payoutId:guid}/process", ProcessPayout)
            .WithName("ProcessPayout")
            .WithSummary("Mark a payout as processed with transaction reference");

        return app;
    }

    private static async Task<IResult> GetEarnings(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerIdClaim = httpContext.User.FindFirst("owner_id")?.Value;

        // Fallback: if owner_id claim not available yet, use sub (restaurant ID)
        // The restaurant's OwnerId will be resolved via SharedKernel
        var restaurantId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        // For now, we need to resolve the owner ID from the restaurant
        // TODO: After login is updated to use owner accounts, use owner_id claim directly
        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(Guid.Parse(restaurantId), ct);
        if (restaurant?.OwnerId is null)
            return Results.NotFound(new { error = "Restaurant owner not found" });

        var query = new GetRestaurantEarningsQuery { OwnerId = restaurant.OwnerId.Value };
        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }

    private static async Task<IResult> GetPayoutHistory(
        HttpContext httpContext,
        IMediator mediator,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var restaurantId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(Guid.Parse(restaurantId), ct);
        if (restaurant?.OwnerId is null)
            return Results.NotFound(new { error = "Restaurant owner not found" });

        var query = new GetPayoutsByOwnerQuery
        {
            OwnerId = restaurant.OwnerId.Value,
            Page = page,
            PageSize = pageSize
        };

        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }

    private static async Task<IResult> GetPendingPayouts(
        IMediator mediator,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetPendingPayoutsQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }

    private static async Task<IResult> ProcessPayout(
        Guid payoutId,
        ProcessPayoutRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ProcessPayoutCommand
        {
            PayoutId = payoutId,
            TransactionReference = request.TransactionReference,
            Notes = request.Notes
        };

        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new { message = "Payout processed successfully" })
            : Results.BadRequest(new { error = result.Error.Message });
    }
    private static async Task<IResult> GetPayoutDetail(
        Guid payoutId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var restaurantId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(Guid.Parse(restaurantId), ct);
        if (restaurant?.OwnerId is null)
            return Results.NotFound(new { error = "Restaurant owner not found" });

        var query = new GetPayoutDetailQuery
        {
            PayoutId = payoutId,
            OwnerId = restaurant.OwnerId.Value
        };

        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error.Message });
    }

    private static async Task<IResult> GetGstSummary(
        HttpContext httpContext,
        IMediator mediator,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var restaurantId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(Guid.Parse(restaurantId), ct);
        if (restaurant?.OwnerId is null)
            return Results.NotFound(new { error = "Restaurant owner not found" });

        // Default to current month if no range specified
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? new DateOnly(today.Year, today.Month, 1);
        var toDate = to ?? today;

        var query = new GetGstSummaryQuery
        {
            OwnerId = restaurant.OwnerId.Value,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }

    private static async Task<IResult> GetTdsSummary(
        HttpContext httpContext,
        IMediator mediator,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var restaurantId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(restaurantId))
            return Results.Unauthorized();

        var restaurantQueryService = httpContext.RequestServices
            .GetRequiredService<RallyAPI.SharedKernel.Abstractions.Restaurants.IRestaurantQueryService>();

        var restaurant = await restaurantQueryService.GetByIdAsync(Guid.Parse(restaurantId), ct);
        if (restaurant?.OwnerId is null)
            return Results.NotFound(new { error = "Restaurant owner not found" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? new DateOnly(today.Year, today.Month, 1);
        var toDate = to ?? today;

        var query = new GetTdsSummaryQuery
        {
            OwnerId = restaurant.OwnerId.Value,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }
}

public record ProcessPayoutRequest(string TransactionReference, string? Notes);
