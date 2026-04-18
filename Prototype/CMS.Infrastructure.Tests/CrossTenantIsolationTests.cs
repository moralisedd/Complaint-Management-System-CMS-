using CMS.Domain.Entities;
using CMS.Domain.ValueObjects;
using CMS.Infrastructure.Data;
using CMS.Infrastructure.Middleware;
using CMS.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CMS.Infrastructure.Tests;

// This test proves that schema-per-tenant isolation actually works at the database level.
// I spin up a real PostgreSQL container (via Testcontainers) so this isn't just
// mocking — it's hitting actual SQL with real schema separation.
public sealed class CrossTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("cms_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // -------------------------------------------------------------------------
    // Lifecycle — start and stop the container around the test run
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await CreateSchemasAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ComplaintWrittenToNatWest_IsNotVisibleToO2Context()
    {
        // Write a complaint into the NatWest schema.
        var natwestComplaintId = await WriteComplaintAsync("natwest", "tenant_natwest");

        // Try to find it from the O2 schema — should be invisible.
        var foundInO2 = await FindComplaintAsync(natwestComplaintId, "o2", "tenant_o2");

        foundInO2.Should().BeNull(
            because: "a complaint in tenant_natwest must never appear in the tenant_o2 context");
    }

    [Fact]
    public async Task ComplaintWrittenToNatWest_IsVisibleWithinNatWestContext()
    {
        // Sanity check — the complaint should be readable from its own schema.
        var complaintId = await WriteComplaintAsync("natwest", "tenant_natwest");
        var found       = await FindComplaintAsync(complaintId, "natwest", "tenant_natwest");

        found.Should().NotBeNull();
        found!.TenantId.Should().Be("natwest");
    }

    [Fact]
    public async Task TwoTenantsCanHaveComplaintsWithoutInterference()
    {
        var natwestId = await WriteComplaintAsync("natwest", "tenant_natwest");
        var o2Id      = await WriteComplaintAsync("o2",      "tenant_o2");

        // Each complaint is readable from its own schema.
        var natwestComplaint = await FindComplaintAsync(natwestId, "natwest", "tenant_natwest");
        var o2Complaint      = await FindComplaintAsync(o2Id, "o2", "tenant_o2");

        natwestComplaint.Should().NotBeNull();
        o2Complaint.Should().NotBeNull();

        // Neither leaks into the other schema.
        var natwestSeenFromO2 = await FindComplaintAsync(natwestId, "o2",      "tenant_o2");
        var o2SeenFromNatwest = await FindComplaintAsync(o2Id,      "natwest", "tenant_natwest");

        natwestSeenFromO2.Should().BeNull(because: "natwest complaint must not appear in o2 schema");
        o2SeenFromNatwest.Should().BeNull(because: "o2 complaint must not appear in natwest schema");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<Guid> WriteComplaintAsync(string tenantId, string searchPath)
    {
        var tenantCtx = new TenantContext { TenantId = tenantId };
        await using var db = CreateDbContext(tenantCtx);

        // Pin the connection so the SET search_path and the subsequent INSERT
        // both run on the same physical connection from the pool.
        // Without this, SaveChangesAsync can get a different connection where
        // search_path was never set, and the INSERT targets the wrong schema.
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync($"SET search_path = {searchPath}, public;");

        var complaint = Complaint.Create(
            tenantId,
            $"Test complaint for {tenantId}",
            "Integration test — cross-tenant isolation.",
            ContactChannel.Web,
            $"test-user-{tenantId}");

        var repo = new EfComplaintRepository(db);
        await repo.AddAsync(complaint);

        return complaint.Id;
    }

    private async Task<Complaint?> FindComplaintAsync(Guid complaintId, string tenantId, string searchPath)
    {
        var tenantCtx = new TenantContext { TenantId = tenantId };
        await using var db = CreateDbContext(tenantCtx);

        // Same connection-pinning pattern as WriteComplaintAsync.
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync($"SET search_path = {searchPath}, public;");

        // IgnoreQueryFilters so we're testing physical row presence, not just the EF filter.
        return await db.Complaints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == complaintId);
    }

    private CmsDbContext CreateDbContext(TenantContext tenantCtx)
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new CmsDbContext(options, tenantCtx);
    }

    private async Task CreateSchemasAsync()
    {
        var dummyCtx = new TenantContext { TenantId = "_setup" };
        await using var db = CreateDbContext(dummyCtx);

        // I issue each DDL statement separately with schema-qualified table names.
        // A single multi-statement ExecuteSqlRawAsync call can't reliably set search_path
        // for subsequent statements — Npgsql's batching doesn't guarantee it.
        // Using explicit schema names (e.g. tenant_natwest.complaints) removes that dependency.
        foreach (var schema in new[] { "tenant_natwest", "tenant_o2" })
        {
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE SCHEMA IF NOT EXISTS {schema}");

            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.complaints (
                    id                UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id         VARCHAR(100)  NOT NULL,
                    subject           VARCHAR(120)  NOT NULL,
                    description       VARCHAR(2000) NOT NULL,
                    channel           VARCHAR(20)   NOT NULL,
                    status            VARCHAR(20)   NOT NULL DEFAULT 'Open',
                    logged_by_user_id VARCHAR(200)  NOT NULL,
                    assigned_to_id    VARCHAR(200),
                    logged_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                    assigned_at       TIMESTAMPTZ,
                    resolved_at       TIMESTAMPTZ,
                    resolution_notes  VARCHAR(4000)
                )");

            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.outbox_events (
                    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id    VARCHAR(100) NOT NULL,
                    event_type   VARCHAR(100) NOT NULL,
                    payload      JSONB        NOT NULL,
                    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    processed_at TIMESTAMPTZ
                )");

            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.audit_events (
                    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    tenant_id     VARCHAR(100) NOT NULL,
                    actor_user_id VARCHAR(200) NOT NULL,
                    action        VARCHAR(100) NOT NULL,
                    resource_type VARCHAR(100) NOT NULL,
                    resource_id   VARCHAR(100) NOT NULL,
                    details       JSONB,
                    occurred_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                )");
        }
    }
}
