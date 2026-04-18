using CMS.Domain.Entities;

namespace CMS.Domain.Interfaces;

/// <summary>
/// Repository abstraction for SupportPerson read access.
/// Used by UC02 Assign Support to populate the tenant-scoped dropdown (Element 10).
/// </summary>
public interface ISupportPersonRepository
{
    Task<IReadOnlyList<SupportPerson>> GetActiveSupportPersonsAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<SupportPerson?> GetByIdAsync(Guid id, string tenantId, CancellationToken ct = default);
}
