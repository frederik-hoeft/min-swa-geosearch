using GeoSearch.Web.Client.Services.Theming;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GeoSearch.Web.Client.UI;

public sealed partial class App : ComponentBase, IDisposable
{
    private MudThemeProvider? _themeProvider;

    [Inject]
    private ThemePreferenceService ThemePreferences { get; set; } = null!;

    protected override void OnInitialized()
    {
        ThemePreferences.Changed += OnThemeChanged;
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _themeProvider is null)
        {
            return;
        }

        bool systemDarkMode = await _themeProvider.GetSystemDarkModeAsync();
        await ThemePreferences.InitializeAsync(systemDarkMode);
        await _themeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChangedAsync);
    }

    private Task OnSystemDarkModeChangedAsync(bool isDarkMode)
    {
        ThemePreferences.ApplySystemPreference(isDarkMode);
        return Task.CompletedTask;
    }

    private void OnThemeChanged(object? sender, EventArgs args) => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        ThemePreferences.Changed -= OnThemeChanged;
        GC.SuppressFinalize(this);
    }
}
