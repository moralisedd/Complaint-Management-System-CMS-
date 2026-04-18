namespace CMS.Application.Complaints;

/// <summary>
/// Abstraction for resolving the industry key of a given tenant.
/// The implementation (Infrastructure layer) reads from appsettings / Redis cache.
/// Decouples the strategy selection from configuration storage.
/// </summary>
public interface ITenantIndustryLookup
{
    /// <summary>Returns the industry key (e.g. "Banking", "Telecom") for the given tenant.</summary>
    Task<string> GetIndustryKeyAsync(string tenantId, CancellationToken ct = default);
}
