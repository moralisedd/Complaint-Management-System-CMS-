using CMS.Domain.Entities;

namespace CMS.Domain.Interfaces;

/// <summary>
/// Repository abstraction for Complaint persistence.
/// Implementations live in CMS.Infrastructure (EfComplaintRepository).
/// The interface belongs in Domain to preserve the Dependency Inversion Principle (Clean Architecture).
/// </summary>
public interface IComplaintRepository
{
    Task<Complaint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Complaint complaint, CancellationToken ct = default);
    Task UpdateAsync(Complaint complaint, CancellationToken ct = default);
    Task<IReadOnlyList<Complaint>> GetByTenantAsync(string tenantId, CancellationToken ct = default);
}
