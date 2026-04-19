using Microsoft.Playwright;

namespace CMS.Web.Tests.Accessibility;

// A shared fixture that initialises Playwright once for the whole test session.
// I used IAsyncLifetime so xUnit calls InitializeAsync before any test runs
// and DisposeAsync after the last one finishes, which avoids browser spin-up costs per test.
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser    Browser    { get; private set; } = null!;

    // The base URL of the running CMS application.
    // Start the app with `dotnet run` in CMS.Web before running these tests.
    public const string BaseUrl = "https://localhost:5001";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Headless Chromium — faster for CI, same rendering engine as Chrome.
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // Ignore dev cert warning for localhost TLS.
            Args = ["--ignore-certificate-errors"]
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
