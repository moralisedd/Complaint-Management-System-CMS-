using CMS.Domain.ValueObjects;

namespace CMS.Domain.Entities;

// The core Complaint entity. I made all state-changing methods live here
// so the business rules are enforced in one place rather than scattered
// across pages or services.
public sealed class Complaint
{
    // Private setters stop anything outside this class from changing state directly.
    // EF Core still works because it uses the private parameterless constructor below.
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;  // which tenant owns this complaint
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ContactChannel Channel { get; private set; }
    public ComplaintStatus Status { get; private set; }
    public string LoggedByUserId { get; private set; } = string.Empty;
    public string? AssignedToId { get; private set; }  // null until a support person is assigned
    public DateTimeOffset LoggedAt { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionNotes { get; private set; }

    // EF Core needs a parameterless constructor to reconstruct objects from the database.
    // I keep it private so nothing else can bypass the factory method.
    private Complaint() { }

    // -------------------------------------------------------------------------
    // Create — the only way to make a new complaint
    // -------------------------------------------------------------------------

    // I used a static factory instead of a public constructor so I can run
    // validation before the object is ever created.
    public static Complaint Create(
        string tenantId,
        string subject,
        string description,
        ContactChannel channel,
        string loggedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(loggedByUserId);

        if (subject.Length > 120)
            throw new ArgumentException("Subject must not exceed 120 characters.", nameof(subject));
        if (description.Length > 2000)
            throw new ArgumentException("Description must not exceed 2000 characters.", nameof(description));

        return new Complaint
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Subject         = subject,
            Description     = description,
            Channel         = channel,
            Status          = ComplaintStatus.Open,
            LoggedByUserId  = loggedByUserId,
            LoggedAt        = DateTimeOffset.UtcNow
        };
    }

    // -------------------------------------------------------------------------
    // AssignTo — moves the complaint from Open → InProgress
    // -------------------------------------------------------------------------

    // I guard against assigning a complaint that's already been picked up.
    // The domain throws so the service layer doesn't need to duplicate this check.
    public void AssignTo(string supportPersonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supportPersonId);

        if (Status != ComplaintStatus.Open)
            throw new InvalidOperationException(
                $"Cannot assign a complaint in status '{Status}'. Only Open complaints can be assigned.");

        AssignedToId = supportPersonId;
        AssignedAt   = DateTimeOffset.UtcNow;
        Status       = ComplaintStatus.InProgress;
    }

    // -------------------------------------------------------------------------
    // Resolve — completes the complaint (out of PoC scope but modelled anyway)
    // -------------------------------------------------------------------------

    public void Resolve(string resolutionNotes)
    {
        if (Status != ComplaintStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot resolve a complaint in status '{Status}'. Only InProgress complaints can be resolved.");

        ResolutionNotes = resolutionNotes;
        ResolvedAt      = DateTimeOffset.UtcNow;
        Status          = ComplaintStatus.Resolved;
    }
}
