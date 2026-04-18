namespace CMS.Domain.Entities;

// Every state-changing operation writes one of these. I never update or delete
// audit records — they're insert-only so there's always a reliable history trail.
public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = string.Empty;
    public string ActorUserId { get; init; } = string.Empty;   // who performed the action
    public string Action { get; init; } = string.Empty;        // e.g. "ComplaintLogged"
    public string ResourceType { get; init; } = string.Empty;  // e.g. "Complaint"
    public string ResourceId { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;       // JSON snapshot of what changed
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
