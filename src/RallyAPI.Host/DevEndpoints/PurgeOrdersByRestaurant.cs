using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.Orders.Infrastructure;

namespace RallyAPI.Host.DevEndpoints;

/// <summary>
/// Developer-only endpoint: hard-deletes orders (by restaurant, by order ID, or both)
/// along with all related rows in the Orders and Delivery modules.
/// Available in Development environment only.
/// </summary>
public static class PurgeOrdersByRestaurant
{
    public record Request(Guid[]? RestaurantIds, Guid[]? OrderIds);

    public record Response(
        int OrdersDeleted,
        int OrderItemsDeleted,
        int DeliveryInfosDeleted,
        int PaymentsDeleted,
        int PayoutLedgersDeleted,
        int PayoutsDeleted,
        int CartsDeleted,
        int DeliveryRequestsDeleted,
        int RiderOffersDeleted,
        int DeliveryQuotesDeleted);

    public static IEndpointRouteBuilder MapPurgeOrdersByRestaurant(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/orders/purge-by-restaurant", HandleAsync)
            .WithName("DevPurgeOrdersByRestaurant")
            .WithTags("Dev")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] Request request,
        [FromServices] IHostEnvironment env,
        [FromServices] OrdersDbContext ordersDb,
        [FromServices] DeliveryDbContext deliveryDb,
        CancellationToken ct)
    {
        if (!env.IsDevelopment())
            return Results.NotFound();

        var restaurantIds = request.RestaurantIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        var requestedOrderIds = request.OrderIds?.Distinct().ToArray() ?? Array.Empty<Guid>();

        if (restaurantIds.Length == 0 && requestedOrderIds.Length == 0)
            return Results.BadRequest(new { error = "Provide at least one of restaurantIds or orderIds" });

        // Resolve the full set of order IDs to purge: union of restaurant-scoped orders and requested order IDs.
        var orderIds = await ordersDb.Orders
            .Where(o => restaurantIds.Contains(o.RestaurantId) || requestedOrderIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToListAsync(ct);

        // Delivery module first (child rows that reference orderIds).
        var riderOffersDeleted = 0;
        var deliveryRequestsDeleted = 0;
        var deliveryQuotesDeleted = 0;

        if (orderIds.Count > 0)
        {
            var deliveryRequestIds = await deliveryDb.DeliveryRequests
                .Where(r => orderIds.Contains(r.OrderId))
                .Select(r => r.Id)
                .ToListAsync(ct);

            if (deliveryRequestIds.Count > 0)
            {
                riderOffersDeleted = await deliveryDb.RiderOffers
                    .Where(o => deliveryRequestIds.Contains(o.DeliveryRequestId))
                    .ExecuteDeleteAsync(ct);
            }

            deliveryRequestsDeleted = await deliveryDb.DeliveryRequests
                .Where(r => orderIds.Contains(r.OrderId))
                .ExecuteDeleteAsync(ct);
        }

        deliveryQuotesDeleted = await deliveryDb.Quotes
            .Where(q =>
                (q.UsedForOrderId.HasValue && orderIds.Contains(q.UsedForOrderId.Value)) ||
                (q.RestaurantId.HasValue && restaurantIds.Contains(q.RestaurantId.Value)))
            .ExecuteDeleteAsync(ct);

        // Orders module (children before parents to avoid FK restrict on PayoutLedger).
        var payoutLedgersDeleted = 0;
        var paymentsDeleted = 0;
        var deliveryInfosDeleted = 0;
        var orderItemsDeleted = 0;
        var ordersDeleted = 0;

        if (orderIds.Count > 0)
        {
            payoutLedgersDeleted = await ordersDb.PayoutLedgers
                .Where(l => orderIds.Contains(l.OrderId))
                .ExecuteDeleteAsync(ct);

            paymentsDeleted = await ordersDb.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .ExecuteDeleteAsync(ct);

            deliveryInfosDeleted = await ordersDb.DeliveryInfos
                .Where(d => orderIds.Contains(EF.Property<Guid>(d, "OrderId")))
                .ExecuteDeleteAsync(ct);

            orderItemsDeleted = await ordersDb.OrderItems
                .Where(i => orderIds.Contains(EF.Property<Guid>(i, "OrderId")))
                .ExecuteDeleteAsync(ct);

            ordersDeleted = await ordersDb.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ExecuteDeleteAsync(ct);
        }

        // Restaurant-wide sweeps only run when restaurantIds was provided.
        var payoutsDeleted = 0;
        var cartsDeleted = 0;
        if (restaurantIds.Length > 0)
        {
            payoutsDeleted = await ordersDb.Payouts
                .Where(p => restaurantIds.Contains(p.OwnerId))
                .ExecuteDeleteAsync(ct);

            cartsDeleted = await ordersDb.Carts
                .Where(c => restaurantIds.Contains(c.RestaurantId))
                .ExecuteDeleteAsync(ct);
        }

        return Results.Ok(new Response(
            OrdersDeleted: ordersDeleted,
            OrderItemsDeleted: orderItemsDeleted,
            DeliveryInfosDeleted: deliveryInfosDeleted,
            PaymentsDeleted: paymentsDeleted,
            PayoutLedgersDeleted: payoutLedgersDeleted,
            PayoutsDeleted: payoutsDeleted,
            CartsDeleted: cartsDeleted,
            DeliveryRequestsDeleted: deliveryRequestsDeleted,
            RiderOffersDeleted: riderOffersDeleted,
            DeliveryQuotesDeleted: deliveryQuotesDeleted));
    }
}
