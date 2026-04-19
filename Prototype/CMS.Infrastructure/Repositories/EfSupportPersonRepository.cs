using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Repositories;

// EF Core implementation of ISupportPersonRepository.
// I only return active support persons ordered by name — that's all the assignment
// dropdown needs. Tenant isolation is handled by the search_path interceptor and
// the global query filter in CmsDbContext, so I never need to filter by tenant here manually.
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
