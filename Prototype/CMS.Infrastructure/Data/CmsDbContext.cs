using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Data;

// The EF Core entry point. I map every entity to its table and configure
// global query filters so tenants can never accidentally read each other's data.
// Tenant isolation works at two levels:
//   1. TenantSchemaInterceptor sets search_path per connection (right schema).
//   2. Global query filters here check TenantId on every SELECT (belt-and-braces).
public sealed class CmsDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public DbSet<Complaint> Complaints { get; set; } = null!;
    public DbSet<SupportPerson> SupportPersons { get; set; } = null!;
    public DbSet<OutboxEvent> OutboxEvents { get; set; } = null!;
    public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

    public CmsDbContext(DbContextOptions<CmsDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ------------------------------------------------------------------
        // Complaint
        // ------------------------------------------------------------------
        mb.Entity<Complaint>(e =>
        {
            e.ToTable("complaints");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(c => c.Subject).HasColumnName("subject").HasMaxLength(120).IsRequired();
            e.Property(c => c.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
            e.Property(c => c.Channel).HasColumnName("channel")
                .HasConversion<string>().IsRequired();
            e.Property(c => c.Status).HasColumnName("status")
                .HasConversion<string>().IsRequired();
            e.Property(c => c.LoggedByUserId).HasColumnName("logged_by_user_id").IsRequired();
            e.Property(c => c.AssignedToId).HasColumnName("assigned_to_id");
            e.Property(c => c.LoggedAt).HasColumnName("logged_at");
            e.Property(c => c.AssignedAt).HasColumnName("assigned_at");
            e.Property(c => c.ResolvedAt).HasColumnName("resolved_at");
            e.Property(c => c.ResolutionNotes).HasColumnName("resolution_notes").HasMaxLength(4000);

            // Every query is automatically filtered to the current tenant.
            e.HasQueryFilter(c => c.TenantId == _tenant.TenantId);
        });

        // ------------------------------------------------------------------
        // SupportPerson
        // ------------------------------------------------------------------
        mb.Entity<SupportPerson>(e =>
        {
            e.ToTable("support_persons");
            e.HasKey(sp => sp.Id);
            e.Property(sp => sp.Id).HasColumnName("id");
            e.Property(sp => sp.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(sp => sp.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(sp => sp.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            e.Property(sp => sp.IsActive).HasColumnName("is_active");

            e.HasQueryFilter(sp => sp.TenantId == _tenant.TenantId);
        });

        // ------------------------------------------------------------------
        // OutboxEvent
        // ------------------------------------------------------------------
        mb.Entity<OutboxEvent>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(o => o.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            e.Property(o => o.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
            e.Property(o => o.ProcessedAt).HasColumnName("processed_at");

            e.Ignore(o => o.IsProcessed);
        });

        // ------------------------------------------------------------------
        // AuditEvent — insert-only, never updated or deleted
        // ------------------------------------------------------------------
        mb.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(a => a.ActorUserId).HasColumnName("actor_user_id").IsRequired();
            e.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            e.Property(a => a.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
            e.Property(a => a.ResourceId).HasColumnName("resource_id").HasMaxLength(100).IsRequired();
            e.Property(a => a.Details).HasColumnName("details").HasColumnType("jsonb");
            e.Property(a => a.OccurredAt).HasColumnName("occurred_at");
        });
    }
}
