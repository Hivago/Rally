using MediatR;

namespace RallyAPI.Orders.Application.Abstractions;

/// <summary>
/// Persists an event to the Orders transactional outbox so it is delivered durably
/// (at-least-once, with retries by the OutboxProcessor) instead of being published
/// in-process — where a consumer failure would lose it permanently with no retry.
/// </summary>
public interface IOutboxWriter
{
    Task WriteAsync(INotification @event, CancellationToken cancellationToken = default);
}
