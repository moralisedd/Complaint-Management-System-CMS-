namespace CMS.Domain.Interfaces;

// This interface gives any class access to the current tenant without depending
// on HTTP-specific types. The middleware sets it once per request; repositories
// and services just read it.
public interface ITenantContext
{
    // The tenant ID comes from the 'tid' JWT claim set by Keycloak (e.g. "natwest", "o2").
    string TenantId { get; }

    // Derived from TenantId — used to set the PostgreSQL search_path for schema isolation.
    string SchemaName => $"tenant_{TenantId}";
}
