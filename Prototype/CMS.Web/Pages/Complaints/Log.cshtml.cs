using CMS.Application.Complaints;
using CMS.Domain.Interfaces;
using CMS.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CMS.Web.Pages.Complaints;

// Only Consumers can access this page — the policy checks the 'role' claim.
[Authorize(Policy = "CanLogComplaint")]
public sealed class LogModel : PageModel
{
    private readonly IComplaintService _complaintService;
    private readonly ITenantContext _tenantContext;

    public LogModel(IComplaintService complaintService, ITenantContext tenantContext)
    {
        _complaintService = complaintService;
        _tenantContext    = tenantContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ViewData["AttemptedSubmit"] = true;

        if (!ModelState.IsValid)
            return Page();

        // 'sub' is the standard JWT subject claim — the user's unique ID in Keycloak.
        var userId = User.FindFirst("sub")?.Value
            ?? User.Identity?.Name
            ?? "unknown";

        var command = new LogComplaintCommand(
            TenantId:      _tenantContext.TenantId,
            Subject:       Input.Subject,
            Description:   Input.Description,
            Channel:       Input.Channel,
            LoggedByUserId: userId);

        var result = await _complaintService.LogComplaintAsync(command, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        // Post-Redirect-Get — redirect after a successful POST so hitting F5 doesn't re-submit.
        return RedirectToPage("./Confirmation",
            new { id = result.Value!.ComplaintId, @ref = result.Value.ReferenceNumber });
    }

    // Validation attributes here mirror the domain rules so errors appear inline
    // before the request even reaches the service layer.
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Subject must not be empty.")]
        [MaxLength(120, ErrorMessage = "Subject must not exceed 120 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description must not be empty.")]
        [MaxLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a contact channel.")]
        [Display(Name = "Contact channel")]
        public ContactChannel Channel { get; set; } = ContactChannel.Web;
    }
}
