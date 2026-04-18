using CMS.Domain.ValueObjects;

namespace CMS.Application.Complaints;

/// <summary>
/// Read-only DTO returned to the presentation layer.
/// Avoids exposing the Complaint aggregate root directly across layer boundaries.
/// </summary>
public sealed record ComplaintSummary(
    Guid Id,
    string ReferenceNumber,
    string Subject,
    string Description,
    ContactChannel Channel,
    ComplaintStatus Status,
    string LoggedByUserId,
    string? AssignedToId,
    DateTimeOffset LoggedAt,
    DateTimeOffset? AssignedAt
);
