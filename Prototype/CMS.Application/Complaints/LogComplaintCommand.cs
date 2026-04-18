using CMS.Domain.ValueObjects;

namespace CMS.Application.Complaints;

/// <summary>
/// Input command for UC01 — Log a Complaint (FR1).
/// Populated from the Razor Page model after server-side model validation passes.
/// </summary>
public sealed record LogComplaintCommand(
    string TenantId,
    string Subject,
    string Description,
    ContactChannel Channel,
    string LoggedByUserId
);
