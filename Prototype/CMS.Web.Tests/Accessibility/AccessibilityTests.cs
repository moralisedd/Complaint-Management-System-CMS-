using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace CMS.Web.Tests.Accessibility;

// WCAG 2.2 AA accessibility tests using Playwright + axe-core (ADR-08 / RNF03).
//
// Prerequisites:
//   1. Start the app:  cd CMS.Web && dotnet run
//   2. Sign into NatWest as consumer1 / Password1! via the browser, copy the
//      .AspNetCore.Cookies value, paste it into the CookieValue constant below.
//   3. Run:  dotnet test CMS.Web.Tests
//
// axe-core scans every page for all rules and the tests fail if any violation
// at "serious" or "critical" impact is found (covers WCAG 2.1 + 2.2 AA).
[Collection("Playwright")]
public sealed class AccessibilityTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;

    // Paste a valid cookie from an authenticated browser session here.
    // This avoids having to drive the Keycloak login flow inside the test.
    // Refresh before each test run if the session has expired.
    private const string CookieValue = "chunks-2";
    private const string CookieName  = ".AspNetCore.Cookies";

    public AccessibilityTests(PlaywrightFixture fixture)
        => _fx = fixture;

    // -----------------------------------------------------------------------
    // Helper — create an authenticated browser context
    // -----------------------------------------------------------------------

    private async Task<IBrowserContext> AuthenticatedContextAsync()
    {
        var ctx = await _fx.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Trust the local dev cert so Playwright doesn't abort on TLS errors.
            IgnoreHTTPSErrors = true
        });

        // Inject the authentication cookie so pages load in a logged-in state.
        await ctx.AddCookiesAsync(
        [
            new Cookie
            {
                Name     = CookieName,
                Value    = CookieValue,
                Domain   = "localhost",
                Path     = "/",
                Secure   = true,
                HttpOnly = true
            }
        ]);

        return ctx;
    }

    // -----------------------------------------------------------------------
    // Helper — run axe and return only serious/critical violations
    // -----------------------------------------------------------------------

    private static async Task<List<Deque.AxeCore.Commons.AxeResultItem>> GetCriticalViolationsAsync(IPage page)
    {
        var results = await page.RunAxe();
        return results.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Log Complaint page — FR1 (Consumer role)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LogComplaintPage_HasNoWcagViolations()
    {
        await using var ctx = await AuthenticatedContextAsync();
        var page            = await ctx.NewPageAsync();

        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/Complaints/Log");

        var critical = await GetCriticalViolationsAsync(page);

        critical.Should().BeEmpty(
            because: $"Log Complaint page must pass WCAG 2.2 AA. " +
                     $"Found {critical.Count} violation(s): " +
                     string.Join(", ", critical.Select(v => $"{v.Id} ({v.Impact})")));
    }

    [Fact]
    public async Task LogComplaintPage_FormLabels_AreAssociatedWithInputs()
    {
        // Every form control must have an associated <label> so screen readers
        // can announce what each field is for (WCAG 1.3.1 + 2.4.6).
        await using var ctx = await AuthenticatedContextAsync();
        var page            = await ctx.NewPageAsync();

        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/Complaints/Log");

        var results = await page.RunAxe();

        // Filter to label-related rules only
        var labelViolations = results.Violations
            .Where(v => v.Id is "label" or "label-content-name-mismatch")
            .ToList();

        labelViolations.Should().BeEmpty(
            because: "every form input on Log Complaint must have an associated <label>");
    }

    [Fact]
    public async Task LogComplaintPage_ColourContrast_MeetsWcag_AA()
    {
        // Text must meet a 4.5:1 contrast ratio against its background (WCAG 1.4.3).
        await using var ctx = await AuthenticatedContextAsync();
        var page            = await ctx.NewPageAsync();

        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/Complaints/Log");

        var results = await page.RunAxe();

        var contrastViolations = results.Violations
            .Where(v => v.Id == "color-contrast")
            .ToList();

        contrastViolations.Should().BeEmpty(
            because: "text on Log Complaint must meet WCAG 1.4.3 (4.5:1 contrast ratio)");
    }

    // -----------------------------------------------------------------------
    // Login page — no auth required
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoginPage_HasNoWcagViolations()
    {
        // The login page is public — no cookie needed.
        await using var ctx = await _fx.Browser.NewContextAsync(
            new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/login");

        var critical = await GetCriticalViolationsAsync(page);

        critical.Should().BeEmpty(
            because: "Login page must pass WCAG 2.2 AA — this is the first page any user sees");
    }
}
