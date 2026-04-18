namespace CMS.Application.Complaints;

/// <summary>
/// Returned by IComplaintService.LogComplaintAsync on success.
/// Contains enough data for the confirmation page redirect (UC01 Step 10).
/// </summary>
public sealed record LogComplaintResult(
    Guid ComplaintId,
    string ReferenceNumber   // Human-readable ref, e.g. "CMS-2024-000042"
);
