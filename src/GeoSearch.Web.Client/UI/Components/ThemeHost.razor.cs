using GeoSearch.Web.Client.Services.Theming;
using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Components;

/// <summary>Connects the preference service to the app-wide MudBlazor provider and startup gate.</summary>
public sealed partial class ThemeHost : ComponentBase, IDisposable
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ThemePreferenceService ThemePreferences { get; set; } = null!;

    protected override void OnInitialized()
    {
        ThemePreferences.Changed += OnThemeChanged;
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ThemePreferences.InitializeAsync();
        }
    }

    private void OnThemeChanged(object? sender, EventArgs args) => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        ThemePreferences.Changed -= OnThemeChanged;
        GC.SuppressFinalize(this);
    }
}
