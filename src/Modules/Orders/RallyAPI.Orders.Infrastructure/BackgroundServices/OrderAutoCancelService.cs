// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/BackgroundServices/OrderAutoCancelService.cs
// Purpose: Two-stage background service for unconfirmed orders
//   Stage 1 (EscalationMinutes): Flag order as escalated → notify admin via domain event
//   Stage 2 (HardCancelMinutes): Auto-cancel if still unconfirmed
// Pattern: IHostedService with PeriodicTimer

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Domain.Abstractions;
using RallyAPI.Orders.Domain.Enums;
using RallyAPI.SharedKernel.Abstractions.Restaurants;

namespace RallyAPI.Orders.Infrastructure.BackgroundServices;

public sealed class OrderAutoCancelService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderAutoCancelService> _logger;
    private readonly AutoCancelOptions _options;

    public OrderAutoCancelService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderAutoCancelService> logger,
        IOptions<AutoCancelOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OrderAutoCancelService started. " +
            "Check interval: {IntervalSeconds}s | " +
            "Escalate after: {EscalationMinutes} min | " +
            "Hard cancel after: {HardCancelMinutes} min",
            _options.CheckIntervalSeconds,
            _options.EscalationMinutes,
            _options.HardCancelMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CheckIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessStaleOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderAutoCancelService cycle");
            }
        }
    }

    private async Task ProcessStaleOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var escalationCutoff = now.AddMinutes(-_options.EscalationMinutes);
        var hardCancelCutoff = now.AddMinutes(-_options.HardCancelMinutes);

        // Get all orders still in Paid status older than the escalation threshold
        var staleOrders = await orderRepository.GetOrdersByStatusOlderThanAsync(
            OrderStatus.Paid,
            escalationCutoff,
            cancellationToken);

        if (staleOrders.Count == 0)
            return;

        var escalatedCount = 0;
        var cancelledCount = 0;

        foreach (var order in staleOrders)
        {
            try
            {
                if (order.CreatedAt < hardCancelCutoff)
                {
                    // ── STAGE 2: Hard cancel ──
                    order.Cancel(CancellationReason.RestaurantUnavailable, order.RestaurantId , "Auto-cancelled: Restaurant did not confirm within time limit");
                    cancelledCount++;

                    _logger.LogWarning(
                        "AUTO-CANCELLED order {OrderId} ({OrderNumber}). " +
                        "Restaurant {RestaurantId} did not confirm. " +
                        "Order age: {AgeMinutes:F1} minutes",
                        order.Id,
                        order.OrderNumber,
                        order.RestaurantId,
                        (now - order.CreatedAt).TotalMinutes);
                }
                else if (!order.IsEscalated)
                {
                    // ── STAGE 1: Escalate to admin ──
                    order.EscalateToAdmin(
                        $"Restaurant has not confirmed order after {_options.EscalationMinutes} minute(s). " +
                        $"Will auto-cancel at {order.CreatedAt.AddMinutes(_options.HardCancelMinutes):HH:mm:ss} UTC.");
                    escalatedCount++;

                    _logger.LogWarning(
                        "ESCALATED order {OrderId} ({OrderNumber}) to admin. " +
                        "Restaurant {RestaurantId} has not confirmed after {EscalationMinutes} min. " +
                        "Hard cancel in {RemainingMinutes:F1} min",
                        order.Id,
                        order.OrderNumber,
                        order.RestaurantId,
                        _options.EscalationMinutes,
                        _options.HardCancelMinutes - (now - order.CreatedAt).TotalMinutes);
                }
                // else: already escalated, not yet at hard cancel threshold → skip
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process stale order {OrderId}. Status: {Status}, IsEscalated: {IsEscalated}",
                    order.Id,
                    order.Status,
                    order.IsEscalated);
            }
        }

        if (escalatedCount > 0 || cancelledCount > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auto-cancel cycle complete. Escalated: {Escalated}, Cancelled: {Cancelled}",
                escalatedCount,
                cancelledCount);
        }
    }
}