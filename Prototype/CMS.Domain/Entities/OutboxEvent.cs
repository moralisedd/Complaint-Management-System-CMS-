namespace CMS.Domain.Entities;

// I use the transactional outbox pattern to handle notifications reliably.
// Instead of calling an email/SMS service directly (which could fail after
// the database write), I write this record in the same transaction as the
// business change. A background worker picks it up later and sends the notification.
public sealed class OutboxEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;  // e.g. "ComplaintLogged", "SupportAssigned"
    public string Payload { get; init; } = string.Empty;    // JSON payload for the worker to process
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }        // set by the worker once sent
    public bool IsProcessed => ProcessedAt.HasValue;
}
