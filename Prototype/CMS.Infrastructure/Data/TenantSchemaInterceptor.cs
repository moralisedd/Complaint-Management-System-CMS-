using System.Data.Common;
using CMS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.Infrastructure.Data;

// Runs SET search_path on every new database connection so PostgreSQL reads
// from the right tenant schema (e.g. tenant_natwest). This is the first line
// of tenant isolation — the global query filter in CmsDbContext is the second.
//
// I read ITenantContext via IHttpContextAccessor so I get the same scoped instance
// that TenantResolverMiddleware already populated. If I created a new DI scope
// here instead, I'd get a blank TenantId and the search_path would never be set.
public sealed class TenantSchemaInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantSchemaInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken ct = default)
    {
        var tenantContext = _httpContextAccessor.HttpContext?
            .RequestServices.GetService<ITenantContext>();

        if (tenantContext is null) return;

        try
        {
            // e.g. SET search_path = tenant_natwest, public;
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET search_path = {tenantContext.SchemaName}, public;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // TenantId not set yet (unauthenticated or background) — skip silently.
        }
    }
}
