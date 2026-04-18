using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMS.Web.Pages;

// Home page — just confirms the user is authenticated and shows the nav.
[Authorize]
public sealed class IndexModel : PageModel
{
    public void OnGet() { }
}
