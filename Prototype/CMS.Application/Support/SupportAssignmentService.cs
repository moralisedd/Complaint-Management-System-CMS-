using CMS.Application.Common;
using CMS.Domain.Entities;
using CMS.Domain.Interfaces;

namespace CMS.Application.Support;

// Handles the Assign Support Person use case (UC02).
// I put the assignment logic here rather than in the page model or controller
// so both the Razor Pages UI and the REST API can share it without duplication.
public sealed class SupportAssignmentService : ISupportAssignmentService
{
    private readonly IComplaintRepository _complaints;
    private readonly ISupportPersonRepository _supportPersons;

    public SupportAssignmentService(
        IComplaintRepository complaints,
        ISupportPersonRepository supportPersons)
    {
        _complaints     = complaints;
        _supportPersons = supportPersons;
    }

    public async Task<Result> AssignAsync(AssignSupportCommand command, CancellationToken ct = default)
    {
        // 1. Load the complaint — the EF global query filter already restricts
        //    results to the current tenant, so cross-tenant reads are blocked at the DB level.
        var complaint = await _complaints.GetByIdAsync(command.ComplaintId, ct);

        if (complaint is null)
            return Result.Fail($"Complaint {command.ComplaintId} was not found.");

        // 2. Extra tenant check just in case the filter is ever bypassed.
        if (complaint.TenantId != command.TenantId)
            return Result.Fail("Access denied: this complaint belongs to a different tenant.");

        // 3. Make sure the support person exists and is active in this tenant.
        var supportPerson = await _supportPersons.GetByIdAsync(
            command.SupportPersonId, command.TenantId, ct);

        if (supportPerson is null)
            return Result.Fail($"Support person {command.SupportPersonId} not found.");

        // 4. The domain method enforces the Open-only business rule.
        try { complaint.AssignTo(supportPerson.Id.ToString()); }
        catch (InvalidOperationException ex) { return Result.Fail(ex.Message); }

        // 5. Persist — the repository writes the update and the outbox event atomically.
        await _complaints.UpdateAsync(complaint, ct);

        return Result.Ok();
    }

    public async Task<IReadOnlyList<SupportPerson>> GetAvailableSupportPersonsAsync(
        string tenantId,
        CancellationToken ct = default)
        => await _supportPersons.GetActiveSupportPersonsAsync(tenantId, ct);
}
