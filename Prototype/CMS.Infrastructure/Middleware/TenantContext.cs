using CMS.Domain.Interfaces;

namespace CMS.Infrastructure.Middleware;

// Scoped per HTTP request. TenantResolverMiddleware sets TenantId once,
// then anything in that request (repositories, services) can read it.
// I throw if something tries to read it before the middleware has run —
// that's a wiring mistake I'd rather catch immediately than silently ignore.
public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string TenantId
    {
        get => _tenantId
            ?? throw new InvalidOperationException(
                "TenantId hasn't been set yet. Make sure TenantResolverMiddleware runs before any endpoint.");
        set => _tenantId = value;
    }
}
