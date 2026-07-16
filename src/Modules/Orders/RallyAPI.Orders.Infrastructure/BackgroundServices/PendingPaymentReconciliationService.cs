// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/BackgroundServices/PendingPaymentReconciliationService.cs
// Purpose: Safety net for lost payment confirmations.
//
// PayU confirms a payment through three paths: the S2S webhook, the browser
// return POST to surl/furl, and the frontend /verify call. If ALL of those
// fail to reach us (customer closes the tab AND the webhook isn't delivered),
// an order that was actually paid stays stuck in Pending — the restaurant sees
// "payment pending" and OrderAutoCancelService will eventually cancel it while
// the customer has been charged.
//
// This service closes that gap: it periodically finds Pending orders that have
// an in-flight payment and asks PayU directly (verify_payment) whether the
// money was actually collected, confirming the order if so. It is the ultimate
// backstop that does not depend on any callback reaching us.
//
// Pattern: BackgroundService + PeriodicTimer (matches OrderAutoCancelService).

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.Orders.Domain.Repositories;

namespace RallyAPI.Orders.Infrastructure.BackgroundServices;

public sealed class PendingPaymentReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingPaymentReconciliationService> _logger;
    private readonly int _intervalSeconds;
    private readonly int _minAgeSeconds;

    public PendingPaymentReconciliationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PendingPaymentReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // How often to sweep, and how old a Pending order must be before we ask
        // PayU (the delay avoids racing the synchronous browser-return path).
        _intervalSeconds = configuration.GetValue("PaymentReconciliation:IntervalSeconds", 60);
        _minAgeSeconds = configuration.GetValue("PaymentReconciliation:MinAgeSeconds", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PendingPaymentReconciliationService started. Interval: {IntervalSeconds}s | Min order age: {MinAgeSeconds}s",
            _intervalSeconds, _minAgeSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PendingPaymentReconciliationService cycle");
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var payUService = scope.ServiceProvider.GetRequiredService<IPayUService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoff = DateTime.UtcNow.AddSeconds(-_minAgeSeconds);
        var pendingOrders = await orderRepository.GetOrdersByStatusOlderThanAsync(
            OrderStatus.Pending, cutoff, ct);

        if (pendingOrders.Count == 0)
            return;

        var recovered = 0;

        foreach (var order in pendingOrders)
        {
            try
            {
                var payment = await paymentRepository.GetByOrderIdAsync(order.Id, ct);

                // No payment initiated, or already resolved — nothing to reconcile.
                if (payment is null
                    || payment.Status == PaymentStatus.Paid
                    || payment.Status == PaymentStatus.Failed)
                    continue;

                // Ask PayU directly whether the money was actually collected.
                var verify = await payUService.VerifyPaymentAsync(payment.TxnId);
                if (verify is null)
                    continue; // PayU unreachable / no record yet — try again next cycle.

                if (!string.Equals(verify.Status, "success", StringComparison.OrdinalIgnoreCase))
                    continue; // Genuinely not paid — leave for the auto-cancel flow.

                // PayU says PAID but our order is still Pending → recover it.
                payment.MarkSuccess(verify.PayuId, verify.Mode ?? "", verify.BankRefNum);

                if (order.Status == OrderStatus.Pending)
                    order.ConfirmPayment(payment.TxnId, verify.PayuId);

                await unitOfWork.SaveChangesAsync(ct);
                recovered++;

                _logger.LogWarning(
                    "RECONCILED lost payment for Order {OrderId} ({OrderNumber}), TxnId {TxnId}. " +
                    "PayU reported success but order was still Pending — now confirmed to Paid.",
                    order.Id, order.OrderNumber, payment.TxnId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to reconcile payment for Order {OrderId} ({OrderNumber})",
                    order.Id, order.OrderNumber);
            }
        }

        if (recovered > 0)
            _logger.LogInformation("Payment reconciliation cycle complete. Recovered {Count} paid order(s).", recovered);
    }
}
