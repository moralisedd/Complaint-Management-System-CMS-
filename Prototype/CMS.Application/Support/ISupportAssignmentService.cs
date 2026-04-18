using CMS.Application.Common;
using CMS.Domain.Entities;

namespace CMS.Application.Support;

/// <summary>
/// Application service interface for UC02 — Assign Support Person (FR2).
/// </summary>
public interface ISupportAssignmentService
{
    /// <summary>
    /// Assigns a support person to an open complaint.
    /// Transitions complaint status from Open → InProgress (Complaint.AssignTo domain method).
    /// Writes an OUTBOX_EVENT for the NotificationWorker.
    /// </summary>
    Task<Result> AssignAsync(AssignSupportCommand command, CancellationToken ct = default);

    /// <summary>Returns the list of active support persons in the tenant (for the dropdown).</summary>
    Task<IReadOnlyList<SupportPerson>> GetAvailableSupportPersonsAsync(
        string tenantId,
        CancellationToken ct = default);
}
