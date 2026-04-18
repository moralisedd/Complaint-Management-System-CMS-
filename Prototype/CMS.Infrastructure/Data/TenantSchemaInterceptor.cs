using System.Data.Common;
using CMS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.Infrastructure.Data;

// Every time EF Core opens a database connection, I run SET search_path so
// PostgreSQL looks in the right tenant schema first (e.g. tenant_natwest).
// This is the first line of tenant isolation — the global query filter in
// CmsDbContext is the second.
//
// I resolve ITenantContext from the current HTTP request's DI scope via
// IHttpContextAccessor. This is the same instance that TenantResolverMiddleware
// already populated, so TenantId is guaranteed to be set. Creating a new DI
// scope would give a fresh blank context and search_path would never apply.
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
