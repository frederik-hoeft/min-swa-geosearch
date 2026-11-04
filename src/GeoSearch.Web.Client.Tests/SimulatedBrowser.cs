using GeoSearch.Web.Client.Services.Theming;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Tests;

internal sealed class SimulatedBrowser : IJSRuntime, IJSObjectReference
{
    private DotNetObjectReference<ThemePreferenceService>? _observer;

    public string? StoredTheme { get; set; }

    public bool SystemDarkMode { get; set; }

    public bool StorageUnavailable { get; set; }

    public bool IsObserving => _observer is not null;

    public void ChangeSystemTheme(bool isDarkMode)
    {
        SystemDarkMode = isDarkMode;
        _observer?.Value.OnSystemDarkModeChanged(isDarkMode);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        object? result = identifier switch
        {
            "localStorage.getItem" when !StorageUnavailable => StoredTheme,
            "localStorage.setItem" when !StorageUnavailable => SaveTheme(args!),
            "localStorage.getItem" or "localStorage.setItem" => throw new JSException("Storage unavailable"),
            "import" => this,
            "observeSystemDarkMode" => Observe(args!),
            "getCurrentDarkMode" => SystemDarkMode,
            "dispose" => StopObserving(),
            "mudThemeProvider.stopWatchingDarkMode" => null,
            _ => throw new InvalidOperationException($"Unexpected JavaScript call: {identifier}"),
        };

        return ValueTask.FromResult(result is null ? default! : (TValue)result);
    }

    public ValueTask DisposeAsync()
    {
        _observer = null;
        return ValueTask.CompletedTask;
    }

    private object? SaveTheme(object?[] args)
    {
        StoredTheme = (string)args[1]!;
        return null;
    }

    private IJSObjectReference Observe(object?[] args)
    {
        _observer = (DotNetObjectReference<ThemePreferenceService>)args[0]!;
        return this;
    }

    private object? StopObserving()
    {
        _observer = null;
        return null;
    }
}
