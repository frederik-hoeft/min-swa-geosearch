using GeoSearch.Web.Client.Services.Theming;
using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Components;

public sealed partial class ThemeToggle : ComponentBase, IDisposable
{
    [Inject]
    private ThemePreferenceService ThemePreferences { get; set; } = null!;

    protected override void OnInitialized()
    {
        ThemePreferences.Changed += OnThemeChanged;
    }

    private Task ToggleAsync() => ThemePreferences.ToggleAsync();

    private void OnThemeChanged(object? sender, EventArgs args) => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        ThemePreferences.Changed -= OnThemeChanged;
        GC.SuppressFinalize(this);
    }
}
