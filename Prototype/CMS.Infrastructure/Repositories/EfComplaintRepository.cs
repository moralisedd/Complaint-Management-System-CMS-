using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Repositories;

// EF Core implementation of IComplaintRepository.
// The two most important things happening here are:
//   1. Transactional outbox — the OutboxEvent and AuditEvent are written in the
//      same SaveChanges() as the complaint, so they either all succeed or all fail.
//   2. Tenant isolation — search_path is set by TenantSchemaInterceptor before
//      any query runs, and the global query filter in CmsDbContext double-checks TenantId.
public sealed class EfComplaintRepository : IComplaintRepository
{
    private readonly CmsDbContext _db;

    public EfComplaintRepository(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<Complaint?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Complaints
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Complaint complaint, CancellationToken ct = default)
    {
        await _db.Complaints.AddAsync(complaint, ct);

        // Write an outbox event so the notification worker can send an email/SMS
        // without calling an external service directly inside this request.
        var outboxEvent = new OutboxEvent
        {
            TenantId = complaint.TenantId,
            EventType = "ComplaintLogged",
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                ComplaintId = complaint.Id,
                TenantId = complaint.TenantId,
                LoggedByUserId = complaint.LoggedByUserId,
                Channel = complaint.Channel.ToString(),
                LoggedAt = complaint.LoggedAt
            })
        };

        await _db.OutboxEvents.AddAsync(outboxEvent, ct);

        // Write an audit record so there's always a trail of who logged what.
        var auditEvent = new AuditEvent
        {
            TenantId = complaint.TenantId,
            ActorUserId = complaint.LoggedByUserId,
            Action = "ComplaintLogged",
            ResourceType = "Complaint",
            ResourceId = complaint.Id.ToString(),
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                Subject = complaint.Subject,
                Channel = complaint.Channel.ToString()
            })
        };

        await _db.AuditEvents.AddAsync(auditEvent, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Complaint complaint, CancellationToken ct = default)
    {
        _db.Complaints.Update(complaint);

        // Only write the assignment outbox event when a support person has actually been set.
        if (complaint.AssignedToId is not null)
        {
            var outboxEvent = new OutboxEvent
            {
                TenantId = complaint.TenantId,
                EventType = "SupportAssigned",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ComplaintId = complaint.Id,
                    TenantId = complaint.TenantId,
                    AssignedToId = complaint.AssignedToId,
                    AssignedAt = complaint.AssignedAt
                })
            };
            await _db.OutboxEvents.AddAsync(outboxEvent, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Complaint>> GetByTenantAsync(string tenantId, CancellationToken ct = default)
        => await _db.Complaints
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LoggedAt)
            .ToListAsync(ct);
}
