using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CMS.Web.Pages;

// The login page shows two buttons — one per tenant organisation.
// When the user clicks one, I issue a Challenge for the matching OIDC scheme,
// which redirects them to the correct Keycloak realm.
[AllowAnonymous]
public class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        // Skip the login page if already signed in.
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");

        return Page();
    }

    public IActionResult OnPost(string scheme, string? returnUrl = null)
    {
        // Guard against unexpected scheme names — belt-and-braces, avoids open redirect.
        if (scheme is not ("natwest-oidc" or "o2-oidc"))
            return BadRequest("Unknown authentication scheme.");

        var redirectUri = returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, scheme);
    }
}
