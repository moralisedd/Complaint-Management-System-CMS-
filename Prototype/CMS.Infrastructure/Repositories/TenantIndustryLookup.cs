using CMS.Application.Complaints;
using Microsoft.Extensions.Configuration;

namespace CMS.Infrastructure.Repositories;

// Reads the industry key (Banking / Telecom) for a tenant from appsettings.json.
// I kept this in config for the PoC — in production it would hit a tenant-config
// service or a Redis cache rather than a local file.
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
