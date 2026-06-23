namespace RallyAPI.Orders.Infrastructure.Outbox;

/// <summary>
/// A serialized event persisted in the transactional outbox. Writing this row in the
/// same flow that produced the event — and delivering it asynchronously with retries —
/// guarantees at-least-once delivery to cross-module consumers even if the process
/// crashes or a consumer fails. Without it, an in-process publish that throws (e.g. the
/// Delivery handler failing) silently loses the event with no retry.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    /// <summary>Assembly-qualified type name of the event, used to deserialize and republish it.</summary>
    public string Type { get; private set; }

    /// <summary>JSON-serialized event payload (stored as jsonb).</summary>
    public string Content { get; private set; }

    public DateTimeOffset OccurredOn { get; private set; }

    /// <summary>Null until the event has been successfully published to all handlers.</summary>
    public DateTimeOffset? ProcessedOn { get; private set; }

    public int RetryCount { get; private set; }

    /// <summary>Last failure message, for diagnostics / dead-letter inspection.</summary>
    public string? Error { get; private set; }

    // EF
    private OutboxMessage()
    {
        Type = null!;
        Content = null!;
    }

    public OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredOn)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOn = occurredOn;
    }

    public void MarkProcessed(DateTimeOffset processedOn)
    {
        ProcessedOn = processedOn;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        RetryCount++;
        Error = error;
    }
}
