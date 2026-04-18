using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMS.Web.Pages.Complaints;

[Authorize(Policy = "CanLogComplaint")]
public sealed class ConfirmationModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true, Name = "ref")]
    public string ReferenceNumber { get; set; } = string.Empty;

    public void OnGet() { }
}
