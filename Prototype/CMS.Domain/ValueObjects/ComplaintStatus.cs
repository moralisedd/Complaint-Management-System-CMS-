namespace CMS.Domain.ValueObjects;

/// <summary>
/// Enumeration of valid complaint lifecycle states.
/// Maps directly to the STATUS column in the COMPLAINT table (ADR-02A).
/// </summary>
public enum ComplaintStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}
