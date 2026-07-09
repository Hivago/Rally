using System.Text.Json;
using MediatR;
using RallyAPI.Orders.Application.Abstractions;
using RallyAPI.Orders.Infrastructure;

namespace RallyAPI.Orders.Infrastructure.Outbox;

/// <summary>
/// Writes events to the Orders outbox table. Persists immediately so the message is
/// durable the moment this returns; the <see cref="BackgroundServices.OutboxProcessor"/>
/// then publishes it. The event type is stored as its assembly-qualified name so the
/// processor can reconstruct and republish it.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly OrdersDbContext _context;

    public OutboxWriter(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task WriteAsync(INotification @event, CancellationToken cancellationToken = default)
    {
        var type = @event.GetType();

        var message = new OutboxMessage(
            Guid.NewGuid(),
            type.AssemblyQualifiedName!,
            JsonSerializer.Serialize(@event, type),
            DateTimeOffset.UtcNow);

        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
