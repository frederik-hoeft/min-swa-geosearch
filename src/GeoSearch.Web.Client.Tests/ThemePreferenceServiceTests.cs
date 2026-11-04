using GeoSearch.Web.Client.Services.Theming;

namespace GeoSearch.Web.Client.Tests;

[TestClass]
public sealed class ThemePreferenceServiceTests
{
    [TestMethod]
    public async Task SystemPreferenceIsUsedWhenNoExplicitThemeIsSavedAsync()
    {
        await using SimulatedBrowser browser = new() { StoredTheme = "unrecognized", SystemDarkMode = true };
        await using ThemePreferenceService service = new(browser);
        int changes = 0;
        service.Changed += (_, _) => changes++;

        await service.InitializeAsync();
        Assert.IsTrue(service.IsInitialized);
        Assert.IsTrue(service.IsDarkMode);
        Assert.IsTrue(browser.IsObserving);

        browser.ChangeSystemTheme(false);
        Assert.IsFalse(service.IsDarkMode);

        int changesAfterUpdate = changes;
        browser.ChangeSystemTheme(false);
        Assert.AreEqual(changesAfterUpdate, changes);
    }

    [TestMethod]
    public async Task SavedPreferenceOverridesSystemAndTogglePersistsAcrossSessionsAsync()
    {
        await using SimulatedBrowser browser = new() { StoredTheme = "light", SystemDarkMode = true };
        await using ThemePreferenceService service = new(browser);

        await service.InitializeAsync();
        Assert.IsFalse(service.IsDarkMode);

        await service.ToggleAsync();
        Assert.IsTrue(service.IsDarkMode);
        Assert.AreEqual("dark", browser.StoredTheme);

        browser.ChangeSystemTheme(false);
        Assert.IsTrue(service.IsDarkMode);

        await using ThemePreferenceService nextSession = new(browser);
        await nextSession.InitializeAsync();
        Assert.IsTrue(nextSession.IsDarkMode);
    }

    [TestMethod]
    public async Task StorageFailuresDoNotPreventTogglingAsync()
    {
        await using SimulatedBrowser browser = new() { StorageUnavailable = true };
        await using ThemePreferenceService service = new(browser);

        await service.InitializeAsync();
        await service.ToggleAsync();

        Assert.IsTrue(service.IsDarkMode);
        browser.ChangeSystemTheme(false);
        Assert.IsTrue(service.IsDarkMode);
    }

    [TestMethod]
    public async Task UserSelectionDuringInitializationIsNotOverwrittenAsync()
    {
        await using SimulatedBrowser browser = new() { StoredTheme = "light" };
        await using ThemePreferenceService service = new(browser);

        await service.ToggleAsync();
        await service.InitializeAsync();

        Assert.IsTrue(service.IsDarkMode);
    }

    [TestMethod]
    public async Task ObserverIsRemovedWhenServiceIsDisposedAsync()
    {
        await using SimulatedBrowser browser = new();
        ThemePreferenceService service = new(browser);
        await service.InitializeAsync();
        Assert.IsTrue(browser.IsObserving);

        await service.DisposeAsync();
        Assert.IsFalse(browser.IsObserving);
    }
}
