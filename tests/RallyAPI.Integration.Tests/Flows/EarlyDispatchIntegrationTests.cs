using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RallyAPI.Delivery.Application.EventHandlers;
using RallyAPI.Delivery.Application.Services;
using RallyAPI.Delivery.Domain.Abstractions;
using RallyAPI.Delivery.Domain.Entities;
using RallyAPI.Delivery.Domain.Enums;
using RallyAPI.Delivery.Infrastructure.Persistence;
using RallyAPI.Integration.Tests.Infrastructure;
using RallyAPI.SharedKernel.IntegrationEvents.Orders;

namespace RallyAPI.Integration.Tests.Flows;

/// <summary>
/// Early / predictive dispatch — end-to-end against a real Postgres.
///
/// Drives the REAL OrderConfirmedIntegrationEventHandler + REAL repositories (real EF/SQL) so the
/// quote-fee lookup, predictive DispatchAt persistence, quote consumption, and the timing-sensitive
/// GetPendingDispatchAsync / GetStuckForRedispatchAsync queries are all exercised for real. The
/// EarlyDispatchEnabled flag is injected per-scenario so both on and off paths are covered without
/// rebuilding the app (default app config keeps the flag off).
///
/// PrepTime defaults: base 15, +5/item, buffer 5 → for itemCount 1, DispatchAfter = 15 − 5 = 10 min.
/// </summary>
public sealed class EarlyDispatchIntegrationTests : IntegrationTestBase
{
    public EarlyDispatchIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    // ─── Scenario 1: flag ON + quote → priced from quote, predictive DispatchAt, quote consumed ──

    [Fact]
    public async Task AcceptWithEarlyDispatchOnAndQuote_PricesFromQuote_SchedulesPredictiveDispatch()
    {
        var confirmedAt = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        var quoteId = await SeedQuoteAsync(finalFee: 55m, distanceKm: 4m, estimatedMinutes: 22);
        var evt = BuildEvent(quoteId: quoteId, itemCount: 1, confirmedAt: confirmedAt);

        await RunAcceptHandlerAsync(evt, earlyDispatchEnabled: true);

        var request = await GetRequestByOrderAsync(evt.OrderId);
        request.Should().NotBeNull();
        request!.Status.Should().Be(DeliveryRequestStatus.PendingDispatch);
        request.QuotedPrice.Should().Be(55m);
        request.DispatchAt.Should().BeCloseTo(confirmedAt.AddMinutes(10), TimeSpan.FromSeconds(1));
        request.DistanceKm.Should().Be(4m);
        request.EstimatedMinutes.Should().Be(22);

        (await IsQuoteUsedAsync(quoteId)).Should().BeTrue("the quote must be consumed once priced from");
    }

    // ─── Scenario 2: flag OFF → today's behavior, quote untouched ──────────────────────────────

    [Fact]
    public async Task AcceptWithEarlyDispatchOff_UsesReadyTimeBehavior_LeavesQuoteUnused()
    {
        var confirmedAt = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        var quoteId = await SeedQuoteAsync(finalFee: 55m, distanceKm: 4m, estimatedMinutes: 22);
        var evt = BuildEvent(quoteId: quoteId, itemCount: 1, confirmedAt: confirmedAt);

        await RunAcceptHandlerAsync(evt, earlyDispatchEnabled: false);

        var request = await GetRequestByOrderAsync(evt.OrderId);
        request.Should().NotBeNull();
        request!.Status.Should().Be(DeliveryRequestStatus.PendingDispatch);
        request.QuotedPrice.Should().Be(0m);
        request.DispatchAt.Should().BeCloseTo(confirmedAt, TimeSpan.FromSeconds(1));

        (await IsQuoteUsedAsync(quoteId)).Should().BeFalse("flag off must not consume the quote");
    }

    // ─── Scenario 3: flag ON but no quote → fallback ───────────────────────────────────────────

    [Fact]
    public async Task AcceptWithEarlyDispatchOnButNoQuote_FallsBackToReadyTimeDispatch()
    {
        var confirmedAt = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        var evt = BuildEvent(quoteId: null, itemCount: 1, confirmedAt: confirmedAt);

        await RunAcceptHandlerAsync(evt, earlyDispatchEnabled: true);

        var request = await GetRequestByOrderAsync(evt.OrderId);
        request.Should().NotBeNull();
        request!.QuotedPrice.Should().Be(0m);
        request.DispatchAt.Should().BeCloseTo(confirmedAt, TimeSpan.FromSeconds(1));
    }

    // ─── Scenario 4: due-dispatch sweep query fires only once DispatchAt has passed ────────────

    [Fact]
    public async Task GetPendingDispatch_ReturnsRequestOnlyAfterScheduledDispatchAt()
    {
        // DispatchAt 10 min in the future (real-time base so "now" is genuinely before it).
        var confirmedAt = DateTime.UtcNow;
        var quoteId = await SeedQuoteAsync(finalFee: 40m, distanceKm: 3m, estimatedMinutes: 20);
        var evt = BuildEvent(quoteId: quoteId, itemCount: 1, confirmedAt: confirmedAt);

        await RunAcceptHandlerAsync(evt, earlyDispatchEnabled: true);

        // Not yet due — the sweep must NOT pick it up.
        var notDue = await QueryAsync(repo => repo.GetPendingDispatchAsync(DateTime.UtcNow));
        notDue.Should().NotContain(r => r.OrderId == evt.OrderId, "DispatchAt is still ~10 min out");

        // Once its DispatchAt has passed, the sweep returns it.
        var due = await QueryAsync(repo => repo.GetPendingDispatchAsync(confirmedAt.AddMinutes(15)));
        due.Should().Contain(r => r.OrderId == evt.OrderId);
    }

    // ─── Scenario 5: stuck-recovery must NOT front-run a future scheduled DispatchAt ────────────

    [Fact]
    public async Task GetStuckForRedispatch_ExcludesFutureDispatchAt_IncludesPastDispatchAt()
    {
        var now = DateTime.UtcNow;

        // Early-dispatch style: scheduled well into the future.
        var future = await SeedPendingRequestAsync(dispatchAt: now.AddMinutes(30));
        // Ready-time style (flag off): DispatchAt already in the past.
        var past = await SeedPendingRequestAsync(dispatchAt: now.AddMinutes(-1));

        // Simulate the recovery tick 5 min later so the idle (UpdatedAt) threshold is satisfied.
        // createdAfter is a day back so the age floor doesn't exclude the freshly-seeded rows.
        var stuck = await QueryAsync(repo =>
            repo.GetStuckForRedispatchAsync(now.AddMinutes(5), now.AddDays(-1)));

        stuck.Should().Contain(r => r.Id == past, "a past-due PendingDispatch is genuinely stuck");
        stuck.Should().NotContain(r => r.Id == future,
            "a PendingDispatch scheduled for the future must not be front-run by the 2-min net");
    }

    // ─── Scenario 6: stuck-recovery age floor — never resurrect old orders ──────────────────────

    [Fact]
    public async Task GetStuckForRedispatch_ExcludesOrdersOlderThanAgeFloor()
    {
        var now = DateTime.UtcNow;

        var recent = await SeedPendingRequestAsync(dispatchAt: now.AddMinutes(-1));
        var ancient = await SeedPendingRequestAsync(dispatchAt: now.AddMinutes(-1), createdAt: now.AddDays(-40));

        // Age floor at 3h back: the 40-day-old order must be excluded, the fresh one included.
        var stuck = await QueryAsync(repo =>
            repo.GetStuckForRedispatchAsync(now.AddMinutes(5), now.AddHours(-3)));

        stuck.Should().Contain(r => r.Id == recent, "a recent stuck order is recoverable");
        stuck.Should().NotContain(r => r.Id == ancient,
            "an order older than the age floor must never be re-dispatched (the 2026-07-09 backlog bug)");
    }

    // NOTE: a full place-order → confirm → event-pipeline HTTP test is intentionally omitted.
    // Confirm requires OrderStatus.Paid, which now only happens after a PayU webhook (payment
    // hardening) — reaching it needs a simulated webhook unrelated to early dispatch. The real
    // OrderConfirmedIntegrationEventHandler + real repositories are already exercised against a
    // real Postgres by scenarios 1–5 above; the OrderConfirmed event-publish wiring is unchanged
    // by this feature.

    // ─── Helpers ───────────────────────────────────────────────────────────────────────────────

    private async Task RunAcceptHandlerAsync(OrderConfirmedIntegrationEvent evt, bool earlyDispatchEnabled)
    {
        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var handler = new OrderConfirmedIntegrationEventHandler(
            sp.GetRequiredService<IDeliveryRequestRepository>(),
            sp.GetRequiredService<IDeliveryQuoteRepository>(),
            sp.GetRequiredService<PrepTimeCalculator>(),
            sp.GetRequiredService<IUnitOfWork>(),
            Options.Create(new DispatchOptions { EarlyDispatchEnabled = earlyDispatchEnabled }),
            NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.Handle(evt, CancellationToken.None);
    }

    private async Task<Guid> SeedQuoteAsync(decimal finalFee, decimal distanceKm, int estimatedMinutes)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDeliveryQuoteRepository>();
        var quote = DeliveryQuote.CreateOwnFleet(
            id: Guid.NewGuid(),
            pickupLat: 28.6315, pickupLng: 77.2167, pickupPincode: "110001",
            dropLat: 28.6129, dropLng: 77.2295, dropPincode: "110002",
            city: "New Delhi",
            orderAmount: 500m,
            restaurantId: RestaurantId,
            distanceKm: distanceKm,
            baseFee: finalFee,
            finalFee: finalFee,
            estimatedMinutes: estimatedMinutes,
            expiresAt: DateTime.UtcNow.AddMinutes(30));
        await repo.AddAsync(quote);
        return quote.Id;
    }

    private async Task<Guid> SeedPendingRequestAsync(DateTime dispatchAt, DateTime? createdAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDeliveryRequestRepository>();
        var request = DeliveryRequest.Create(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            orderNumber: "RALLY-STUCK",
            quoteId: null,
            quotedPrice: 0m,
            pickupLat: 28.6315, pickupLng: 77.2167, pickupPincode: "110001",
            pickupAddress: "CP", pickupContactName: "R", pickupContactPhone: "+911234567890",
            dropLat: 28.6129, dropLng: 77.2295, dropPincode: "110002",
            dropAddress: "Gate", dropContactName: "C", dropContactPhone: "+919876543210",
            dispatchAt: dispatchAt);
        await repo.AddAsync(request);

        // CreatedAt is stamped to now by the factory; backdate it via raw SQL when a test needs
        // to simulate an old order (the age-floor scenario).
        if (createdAt is not null)
        {
            var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "update delivery.delivery_requests set created_at = {0} where id = {1}",
                createdAt.Value, request.Id);
        }
        return request.Id;
    }

    private async Task<IReadOnlyList<DeliveryRequest>> QueryAsync(
        Func<IDeliveryRequestRepository, Task<IReadOnlyList<DeliveryRequest>>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IDeliveryRequestRepository>());
    }

    private async Task<DeliveryRequest?> GetRequestByOrderAsync(Guid orderId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        return await db.DeliveryRequests.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId);
    }

    private async Task<bool> IsQuoteUsedAsync(Guid quoteId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        return await db.Quotes.AsNoTracking().Where(q => q.Id == quoteId).Select(q => q.IsUsed).FirstAsync();
    }

    private static OrderConfirmedIntegrationEvent BuildEvent(Guid? quoteId, int itemCount, DateTime confirmedAt) =>
        new(
            orderId: Guid.NewGuid(),
            orderNumber: "RALLY-EARLY",
            restaurantId: RestaurantId,
            customerId: CustomerId,
            restaurantName: "Test Restaurant",
            restaurantPhone: "+911234567890",
            pickupAddress: "Connaught Place, New Delhi",
            pickupLatitude: 28.6315,
            pickupLongitude: 77.2167,
            pickupPincode: "110001",
            customerName: "Test Customer",
            customerPhone: "+919876543210",
            dropAddress: "12, India Gate, New Delhi",
            dropLatitude: 28.6129,
            dropLongitude: 77.2295,
            dropPincode: "110002",
            itemCount: itemCount,
            totalAmount: 500m,
            deliveryInstructions: null,
            quoteId: quoteId,
            confirmedAt: confirmedAt,
            isPickupOrder: false);
}
