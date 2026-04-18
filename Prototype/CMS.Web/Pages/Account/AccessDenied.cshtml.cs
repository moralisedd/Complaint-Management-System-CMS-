using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMS.Web.Pages.Account;

/// <summary>
/// Shown when an authenticated user lacks the required role for a page (ADR-05).
/// The cookie auth handler redirects here automatically via AccessDeniedPath = "/Account/AccessDenied".
/// [AllowAnonymous] is required so unauthenticated users reaching this page don't loop back to login.
/// </summary>
[AllowAnonymous]
public sealed class AccessDeniedModel : PageModel
{
    public void OnGet() { }
}
