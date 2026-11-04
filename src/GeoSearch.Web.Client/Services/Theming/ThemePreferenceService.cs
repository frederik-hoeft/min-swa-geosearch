using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Services.Theming;

/// <summary>Owns the active theme and the browser's optional explicit preference.</summary>
public sealed class ThemePreferenceService(IJSRuntime jsRuntime)
{
    private const string STORAGE_KEY = "geosearch.theme";

    private bool? _userPreference;

    public bool IsDarkMode { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Restores the explicit preference, or follows the browser's color scheme.</summary>
    public async Task InitializeAsync(bool systemDarkMode)
    {
        string? storedPreference = null;

        try
        {
            storedPreference = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", STORAGE_KEY);
        }
        catch (JSException)
        {
            // Storage can be disabled by browser privacy settings; the theme still works.
        }

        // A user click during initialization takes precedence over a stale storage read.
        if (_userPreference is not null)
        {
            return;
        }

        _userPreference = storedPreference switch
        {
            "dark" => true,
            "light" => false,
            _ => null,
        };

        SetDarkMode(_userPreference ?? systemDarkMode);
    }

    /// <summary>Follows system changes until the user explicitly selects a theme.</summary>
    public void ApplySystemPreference(bool systemDarkMode)
    {
        if (_userPreference is null)
        {
            SetDarkMode(systemDarkMode);
        }
    }

    public async Task ToggleAsync()
    {
        _userPreference = !IsDarkMode;
        SetDarkMode(_userPreference.Value);

        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", STORAGE_KEY,
                _userPreference.Value ? "dark" : "light");
        }
        catch (JSException)
        {
            // The active theme remains usable even when the preference cannot be saved.
        }
    }

    private void SetDarkMode(bool isDarkMode)
    {
        if (IsDarkMode == isDarkMode)
        {
            return;
        }

        IsDarkMode = isDarkMode;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
