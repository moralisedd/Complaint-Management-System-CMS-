using CMS.Application.Common;

namespace CMS.Application.Complaints;

/// <summary>
/// Application service interface for complaint operations (UC01, FR1).
/// Registered as a scoped service in DI (CMS.Infrastructure implementation).
/// </summary>
public interface IComplaintService
{
    /// <summary>
    /// Logs a new complaint. Selects the tenant resolution strategy, creates the domain entity,
    /// persists to the tenant schema, and writes the transactional outbox event.
    /// </summary>
    Task<Result<LogComplaintResult>> LogComplaintAsync(
        LogComplaintCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a summary of a specific complaint (used by confirmation and detail pages).
    /// Returns Fail if the complaint does not exist in the current tenant schema.
    /// </summary>
    Task<Result<ComplaintSummary>> GetComplaintAsync(
        Guid id,
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all complaints for the given tenant, ordered newest-first.
    /// Used by the complaints list page (Consumers see own; agents/admins see all).
    /// </summary>
    Task<Result<IReadOnlyList<ComplaintSummary>>> GetComplaintsByTenantAsync(
        string tenantId,
        CancellationToken ct = default);
}
