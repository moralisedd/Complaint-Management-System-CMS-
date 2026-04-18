using CMS.Application.Complaints;
using CMS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMS.Web.Pages.Complaints;

[Authorize]
public sealed class ComplaintsIndexModel : PageModel
{
    private readonly IComplaintService _complaintService;
    private readonly ITenantContext _tenantContext;

    public ComplaintsIndexModel(IComplaintService complaintService, ITenantContext tenantContext)
    {
        _complaintService = complaintService;
        _tenantContext    = tenantContext;
    }

    public IReadOnlyList<ComplaintSummary> Complaints { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await _complaintService.GetComplaintsByTenantAsync(_tenantContext.TenantId, ct);

        if (result.IsFailure)
        {
            Complaints = [];
            return;
        }

        var userId     = User.FindFirst("sub")?.Value ?? string.Empty;
        var isConsumer = User.HasClaim("role", "Consumer");

        // Consumers only see their own complaints; agents and admins see everything.
        Complaints = isConsumer
            ? result.Value!.Where(c => c.LoggedByUserId == userId).ToList()
            : result.Value!;
    }
}
