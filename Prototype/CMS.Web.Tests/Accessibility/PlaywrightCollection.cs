namespace CMS.Web.Tests.Accessibility;

// xUnit collection definition — ensures all tests in the "Playwright" collection
// share a single PlaywrightFixture instance and don't run in parallel with each other.
// (Parallel browser contexts on the same browser instance can interfere with cookie state.)
[CollectionDefinition("Playwright")]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
