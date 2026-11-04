using GeoSearch.Web.Client.Services.Theming;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Tests;

[TestClass]
public sealed class ThemePreferenceServiceTests
{
    [TestMethod]
    public async Task SystemPreferenceIsUsedWhenNoExplicitThemeIsSavedAsync()
    {
        MemoryStorageJsRuntime storage = new() { StoredTheme = "unrecognized" };
        ThemePreferenceService service = new(storage);
        int changes = 0;
        service.Changed += (_, _) => changes++;

        await service.InitializeAsync(systemDarkMode: true);
        Assert.IsTrue(service.IsDarkMode);
        Assert.AreEqual(1, changes);

        service.ApplySystemPreference(systemDarkMode: false);
        Assert.IsFalse(service.IsDarkMode);
        Assert.AreEqual(2, changes);

        service.ApplySystemPreference(systemDarkMode: false);
        Assert.AreEqual(2, changes);
    }

    [TestMethod]
    public async Task SavedPreferenceOverridesSystemAndTogglePersistsAcrossSessionsAsync()
    {
        MemoryStorageJsRuntime storage = new() { StoredTheme = "light" };
        ThemePreferenceService service = new(storage);

        await service.InitializeAsync(systemDarkMode: true);
        Assert.IsFalse(service.IsDarkMode);

        await service.ToggleAsync();
        Assert.IsTrue(service.IsDarkMode);
        Assert.AreEqual("dark", storage.StoredTheme);

        service.ApplySystemPreference(systemDarkMode: false);
        Assert.IsTrue(service.IsDarkMode);

        ThemePreferenceService nextSession = new(storage);
        await nextSession.InitializeAsync(systemDarkMode: false);
        Assert.IsTrue(nextSession.IsDarkMode);
    }

    [TestMethod]
    public async Task StorageFailuresDoNotPreventTogglingAsync()
    {
        MemoryStorageJsRuntime storage = new() { Unavailable = true };
        ThemePreferenceService service = new(storage);

        await service.InitializeAsync(systemDarkMode: false);
        await service.ToggleAsync();

        Assert.IsTrue(service.IsDarkMode);
        service.ApplySystemPreference(systemDarkMode: false);
        Assert.IsTrue(service.IsDarkMode);
    }

    [TestMethod]
    public async Task UserSelectionDuringInitializationIsNotOverwrittenAsync()
    {
        MemoryStorageJsRuntime storage = new() { StoredTheme = "light" };
        ThemePreferenceService service = new(storage);

        await service.ToggleAsync();
        await service.InitializeAsync(systemDarkMode: false);

        Assert.IsTrue(service.IsDarkMode);
    }

    private sealed class MemoryStorageJsRuntime : IJSRuntime
    {
        public string? StoredTheme { get; set; }

        public bool Unavailable { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (Unavailable)
            {
                throw new JSException("Storage unavailable");
            }

            if (identifier == "localStorage.getItem")
            {
                return ValueTask.FromResult((TValue)(object?)StoredTheme!);
            }

            if (identifier == "localStorage.setItem")
            {
                StoredTheme = (string)args![1]!;
                return ValueTask.FromResult(default(TValue)!);
            }

            throw new InvalidOperationException($"Unexpected JavaScript call: {identifier}");
        }
    }
}
