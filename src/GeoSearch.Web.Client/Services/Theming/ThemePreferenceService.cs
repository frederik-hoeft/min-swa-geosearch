using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Services.Theming;

/// <summary>Owns the effective theme, the user's optional override, and browser color-scheme updates.</summary>
public sealed class ThemePreferenceService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string STORAGE_KEY = "geosearch.theme";
    private const string OBSERVER_MODULE = "./js/theme-preference.js";

    private bool? _userPreference;
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private DotNetObjectReference<ThemePreferenceService>? _callbackReference;

    public bool IsDarkMode { get; private set; }

    public bool IsInitialized { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Restores the saved selection and subscribes to browser color-scheme changes.</summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

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
        _userPreference ??= storedPreference switch
        {
            "dark" => true,
            "light" => false,
            _ => null,
        };

        try
        {
            _module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", OBSERVER_MODULE);
            _callbackReference = DotNetObjectReference.Create(this);
            _observer = await _module.InvokeAsync<IJSObjectReference>("observeSystemDarkMode", _callbackReference);

            // Read *after* subscribing, so a system change during async initialization isn't missed.
            ApplySystemPreference(await _observer.InvokeAsync<bool>("getCurrentDarkMode"));
        }
        catch (JSException)
        {
            // A browser without a usable observer can still use the saved/manual selection.
        }

        if (_userPreference is not null)
        {
            SetDarkMode(_userPreference.Value);
        }

        IsInitialized = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Receives browser color-scheme notifications without mutating MudBlazor's internal state.</summary>
    [JSInvokable]
    public void OnSystemDarkModeChanged(bool systemDarkMode) => ApplySystemPreference(systemDarkMode);

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

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_observer is not null)
            {
                await _observer.InvokeVoidAsync("dispose");
                await _observer.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The browser may already be navigating away.
        }
        finally
        {
            _callbackReference?.Dispose();
        }
    }
}
