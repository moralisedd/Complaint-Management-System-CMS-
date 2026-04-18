namespace CMS.Application.Support;

/// <summary>
/// Input command for UC02 — Assign Support Person (FR2).
/// Populated from the Assign Support Razor Page after confirmation.
/// </summary>
public sealed record AssignSupportCommand(
    Guid ComplaintId,
    Guid SupportPersonId,
    string TenantId,
    string ActorUserId    // The Help Desk Agent performing the assignment
);
