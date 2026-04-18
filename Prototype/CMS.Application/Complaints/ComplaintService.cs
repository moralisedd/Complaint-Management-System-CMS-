using CMS.Application.Common;
using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Domain.Strategies;

namespace CMS.Application.Complaints;

// Handles the Log a Complaint use case. I kept all the orchestration logic here
// rather than in the page model so the business flow is testable without HTTP.
public sealed class ComplaintService : IComplaintService
{
    private readonly IComplaintRepository _complaints;
    private readonly ResolutionStrategyFactory _strategyFactory;
    private readonly ITenantIndustryLookup _industryLookup;

    public ComplaintService(
        IComplaintRepository complaints,
        ResolutionStrategyFactory strategyFactory,
        ITenantIndustryLookup industryLookup)
    {
        _complaints     = complaints;
        _strategyFactory = strategyFactory;
        _industryLookup  = industryLookup;
    }

    public async Task<Result<LogComplaintResult>> LogComplaintAsync(
        LogComplaintCommand command,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Work out which industry this tenant is in, then pick the matching strategy.
            var industryKey = await _industryLookup.GetIndustryKeyAsync(command.TenantId, ct);
            var strategy    = _strategyFactory.GetStrategy(industryKey);

            // 2. Create the complaint through the domain factory (validates inputs).
            var complaint = Complaint.Create(
                command.TenantId,
                command.Subject,
                command.Description,
                command.Channel,
                command.LoggedByUserId);

            // 3. Run any industry-specific validation (e.g. FCA / Ofcom rules).
            strategy.ValidateComplaint(complaint);

            // 4. Persist — the repository also writes the outbox event in the same transaction.
            await _complaints.AddAsync(complaint, ct);

            return Result<LogComplaintResult>.Ok(
                new LogComplaintResult(complaint.Id, GenerateReference(complaint)));
        }
        catch (ArgumentException ex)
        {
            return Result<LogComplaintResult>.Fail($"Validation error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<LogComplaintResult>.Fail($"Business rule violation: {ex.Message}");
        }
    }

    public async Task<Result<ComplaintSummary>> GetComplaintAsync(
        Guid id,
        string tenantId,
        CancellationToken ct = default)
    {
        var complaint = await _complaints.GetByIdAsync(id, ct);

        // Double-check the tenant even though the EF query filter also enforces it.
        if (complaint is null || complaint.TenantId != tenantId)
            return Result<ComplaintSummary>.Fail($"Complaint {id} not found.");

        return Result<ComplaintSummary>.Ok(MapToSummary(complaint));
    }

    public async Task<Result<IReadOnlyList<ComplaintSummary>>> GetComplaintsByTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var complaints = await _complaints.GetByTenantAsync(tenantId, ct);
            return Result<IReadOnlyList<ComplaintSummary>>.Ok(complaints.Select(MapToSummary).ToList());
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ComplaintSummary>>.Fail($"Could not load complaints: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Generates a human-readable reference number from the complaint's ID and year.
    private static string GenerateReference(Complaint c)
        => $"CMS-{c.LoggedAt.Year}-{c.Id.ToString("N")[..8].ToUpper()}";

    private static ComplaintSummary MapToSummary(Complaint c) =>
        new(c.Id,
            GenerateReference(c),
            c.Subject,
            c.Description,
            c.Channel,
            c.Status,
            c.LoggedByUserId,
            c.AssignedToId,
            c.LoggedAt,
            c.AssignedAt);
}
