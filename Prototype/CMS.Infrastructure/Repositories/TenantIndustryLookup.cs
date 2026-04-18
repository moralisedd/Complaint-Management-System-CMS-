using CMS.Application.Complaints;
using Microsoft.Extensions.Configuration;

namespace CMS.Infrastructure.Repositories;

/// <summary>
/// Reads the industry key for a given tenant from appsettings.json (Tenants section).
/// In production this would query Redis or a tenant-config service.
/// </summary>
public sealed class TenantIndustryLookup : ITenantIndustryLookup
{
    private readonly IReadOnlyDictionary<string, string> _industryMap;

    public TenantIndustryLookup(IConfiguration configuration)
    {
        // Expected appsettings shape:
        //   "Tenants": { "natwest": "Banking", "o2": "Telecom" }
        _industryMap = configuration
            .GetSection("Tenants")
            .GetChildren()
            .ToDictionary(
                s => s.Key,
                s => s.Value ?? "Banking",
                StringComparer.OrdinalIgnoreCase);
    }

    public Task<string> GetIndustryKeyAsync(string tenantId, CancellationToken ct = default)
    {
        if (_industryMap.TryGetValue(tenantId, out var key))
            return Task.FromResult(key);

        throw new InvalidOperationException(
            $"No industry configuration found for tenant '{tenantId}'. " +
            $"Add an entry to the 'Tenants' section in appsettings.json.");
    }
}
