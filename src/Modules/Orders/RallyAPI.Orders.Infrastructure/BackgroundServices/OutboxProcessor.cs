using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RallyAPI.Orders.Infrastructure;
using RallyAPI.Orders.Infrastructure.Outbox;

namespace RallyAPI.Orders.Infrastructure.BackgroundServices;

/// <summary>
/// Drains the Orders outbox: deserializes each pending message and republishes it via
/// MediatR. Delivery is at-least-once — a message is marked processed only after its
/// handlers succeed, so a crash or consumer failure simply retries on the next tick.
/// Consumers must therefore be idempotent (the Delivery OrderConfirmed consumer already
/// skips when a DeliveryRequest exists). After <see cref="MaxRetries"/> failed attempts a
/// message is left in place as a dead-letter (surfaced via logs / Sentry) rather than
/// blocking the queue.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxRetries = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started (poll every {Seconds}s)", PollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing tick failed");
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOn == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type);
                if (eventType is null)
                {
                    message.RecordFailure($"Could not resolve type '{message.Type}'");
                    _logger.LogError("Outbox message {Id}: unresolvable type {Type}", message.Id, message.Type);
                    continue;
                }

                if (JsonSerializer.Deserialize(message.Content, eventType) is not INotification @event)
                {
                    message.RecordFailure($"Type '{message.Type}' is not an INotification");
                    _logger.LogError("Outbox message {Id}: {Type} is not an INotification", message.Id, message.Type);
                    continue;
                }

                await publisher.Publish(@event, ct);
                message.MarkProcessed(DateTimeOffset.UtcNow);

                _logger.LogInformation("Outbox message {Id} ({Type}) delivered", message.Id, eventType.Name);
            }
            catch (Exception ex)
            {
                message.RecordFailure(ex.Message);
                _logger.LogError(ex, "Outbox message {Id} ({Type}) failed (attempt {Retry})",
                    message.Id, message.Type, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
