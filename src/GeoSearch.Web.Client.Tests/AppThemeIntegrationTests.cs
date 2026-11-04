using GeoSearch.Web.Client.Services.Theming;
using GeoSearch.Web.Client.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace GeoSearch.Web.Client.Tests;

[TestClass]
public sealed class AppThemeIntegrationTests
{
    [TestMethod]
    public async Task MudThemeProviderFollowsSystemUntilExplicitUserOverrideAsync()
    {
        await using SimulatedBrowser browser = new();
        await using ThemePreferenceService theme = new(browser);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(theme);
        services.AddSingleton<IJSRuntime>(browser);
        using ServiceProvider provider = services.BuildServiceProvider();

        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        RenderFragment content = builder => builder.AddMarkupContent(0, "<div id=\"theme-ready\">Ready</div>");
        Dictionary<string, object?> parameters = new()
        {
            [nameof(ThemeHost.ChildContent)] = content,
        };
        HtmlRootComponent host = await renderer.Dispatcher.InvokeAsync(
            () => renderer.RenderComponentAsync<ThemeHost>(ParameterView.FromDictionary(parameters)));

        string initialHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        StringAssert.Contains(initialHtml, "Loading GeoSearch...", StringComparison.Ordinal);
        Assert.IsFalse(initialHtml.Contains("theme-ready", StringComparison.Ordinal));

        await theme.InitializeAsync();
        string lightHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        Assert.IsFalse(lightHtml.Contains("gs-theme-loading", StringComparison.Ordinal));
        StringAssert.Contains(lightHtml, "theme-ready", StringComparison.Ordinal);
        StringAssert.Contains(lightHtml, "--mud-palette-background: rgba(243,247,246,1)", StringComparison.OrdinalIgnoreCase);

        browser.ChangeSystemTheme(true);
        string darkHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        StringAssert.Contains(darkHtml, "--mud-palette-background: rgba(16,30,36,1)", StringComparison.OrdinalIgnoreCase);

        await theme.ToggleAsync();
        string manualLightHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        StringAssert.Contains(manualLightHtml, "--mud-palette-background: rgba(243,247,246,1)", StringComparison.OrdinalIgnoreCase);

        browser.ChangeSystemTheme(false);
        browser.ChangeSystemTheme(true);
        string persistedLightHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        StringAssert.Contains(persistedLightHtml, "--mud-palette-background: rgba(243,247,246,1)", StringComparison.OrdinalIgnoreCase);
    }
    [TestMethod]
    public async Task SavedDarkPreferenceIsAppliedBeforeMainUiAppearsAsync()
    {
        await using SimulatedBrowser browser = new() { StoredTheme = "dark", SystemDarkMode = false };
        await using ThemePreferenceService theme = new(browser);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(theme);
        services.AddSingleton<IJSRuntime>(browser);
        using ServiceProvider provider = services.BuildServiceProvider();

        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        HtmlRootComponent host = await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<ThemeHost>);

        string initialHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        StringAssert.Contains(initialHtml, "gs-theme-loading", StringComparison.Ordinal);

        await theme.InitializeAsync();
        string restoredHtml = await renderer.Dispatcher.InvokeAsync(host.ToHtmlString);
        Assert.IsFalse(restoredHtml.Contains("gs-theme-loading", StringComparison.Ordinal));
        StringAssert.Contains(restoredHtml, "--mud-palette-background: rgba(16,30,36,1)", StringComparison.Ordinal);
    }
}
