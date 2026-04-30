namespace RallyAPI.SharedKernel.Domain.Entities;

public class WebhookAuditLog
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty; // 'payu', 'prorouting'
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public string SourceIp { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public bool TimestampValid { get; set; }
    public bool IsDuplicate { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty; // 'accepted', 'rejected_signature', 'rejected_timestamp', 'rejected_duplicate', 'processed', 'failed'
    public string RawBody { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Guid CorrelationId { get; set; }
}
