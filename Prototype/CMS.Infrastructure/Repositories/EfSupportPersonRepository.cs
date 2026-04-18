using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ISupportPersonRepository.
/// Tenant isolation is enforced by both the global query filter and search_path interceptor.
/// The resulting query returns only SupportPersons from the current tenant's schema (UC02 Step 3).
/// </summary>
public sealed class EfSupportPersonRepository : ISupportPersonRepository
{
    private readonly CmsDbContext _db;

    public EfSupportPersonRepository(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SupportPerson>> GetActiveSupportPersonsAsync(
        string tenantId,
        CancellationToken ct = default)
        => await _db.SupportPersons
            .AsNoTracking()
            .Where(sp => sp.IsActive)
            .OrderBy(sp => sp.DisplayName)
            .ToListAsync(ct);

    public async Task<SupportPerson?> GetByIdAsync(
        Guid id,
        string tenantId,
        CancellationToken ct = default)
        => await _db.SupportPersons
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.Id == id && sp.IsActive, ct);
}
