using CMS.Application.Complaints;
using CMS.Application.Support;
using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CMS.Web.Pages.Complaints;

// Only HelpDeskAgents can assign complaints.
[Authorize(Policy = "CanAssignSupport")]
public sealed class AssignModel : PageModel
{
    private readonly ISupportAssignmentService _assignmentService;
    private readonly IComplaintService _complaintService;
    private readonly ITenantContext _tenantContext;

    public AssignModel(
        ISupportAssignmentService assignmentService,
        IComplaintService complaintService,
        ITenantContext tenantContext)
    {
        _assignmentService = assignmentService;
        _complaintService  = complaintService;
        _tenantContext     = tenantContext;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public ComplaintSummary? Complaint { get; private set; }
    public IReadOnlyList<SupportPerson> SupportPersons { get; private set; } = [];

    // Builds the dropdown list for the support person selector.
    public SelectList SupportPersonSelectList => new(
        SupportPersons,
        nameof(SupportPerson.Id),
        nameof(SupportPerson.DisplayName));

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        => await LoadPageDataAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ViewData["AttemptedSubmit"] = true;

        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync(ct);
            return Page();
        }

        var actorUserId = User.FindFirst("sub")?.Value
            ?? User.Identity?.Name
            ?? "unknown";

        var command = new AssignSupportCommand(
            ComplaintId:     Id,
            SupportPersonId: Input.SupportPersonId,
            TenantId:        _tenantContext.TenantId,
            ActorUserId:     actorUserId);

        var result = await _assignmentService.AssignAsync(command, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPageDataAsync(ct);
            return Page();
        }

        // Look up the display name so the success banner shows a proper name, not just a GUID.
        var supportPersons = await _assignmentService.GetAvailableSupportPersonsAsync(
            _tenantContext.TenantId, ct);
        var displayName = supportPersons.FirstOrDefault(sp => sp.Id == Input.SupportPersonId)
                              ?.DisplayName ?? Input.SupportPersonId.ToString();

        // TempData persists across the PRG redirect so the success banner appears on the next GET.
        TempData["SuccessMessage"] = $"Complaint assigned to {displayName}. Reference: {Id}.";

        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult> LoadPageDataAsync(CancellationToken ct)
    {
        var complaintResult = await _complaintService.GetComplaintAsync(
            Id, _tenantContext.TenantId, ct);

        if (complaintResult.IsFailure)
            return NotFound();

        Complaint     = complaintResult.Value;
        SupportPersons = await _assignmentService.GetAvailableSupportPersonsAsync(
            _tenantContext.TenantId, ct);

        return Page();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Please select a support person.")]
        [Display(Name = "Support person")]
        public Guid SupportPersonId { get; set; }
    }
}
