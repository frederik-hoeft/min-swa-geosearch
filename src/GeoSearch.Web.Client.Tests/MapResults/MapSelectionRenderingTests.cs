using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Client.UI.Components.Search;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using System.Reflection;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class MapSelectionRenderingTests
{
    [TestMethod]
    public async Task ExternalMarkerSelectionRendersDetailsWithoutParentParameterChangesAsync()
    {
        SelectionProbe probe = new();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(probe);
        using HttpClient client = new() { BaseAddress = new Uri("https://example.test/") };
        services.AddSingleton(new SearchApiClient(client));
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());

        HtmlRootComponent root = await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);
        Assert.IsNotNull(probe.Workspace);
        GeoJsonFeature first = Feature("osm:1", "First place");
        GeoJsonFeature second = Feature("osm:2", "Second place");
        GeoJsonFeatureCollection results = new("test", [first, second]);
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("places")
            .WithForwardResult("places", new ReadySearch(results));
        typeof(SearchWorkspace).GetField("_searchState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(probe.Workspace, committed);

        // No parent EventCallback is assigned: this reproduces the @ref callback
        // path that previously updated state but did not schedule a child render.
        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(first));
        string firstHtml = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        StringAssert.Contains(firstHtml, "PLACE DETAILS", StringComparison.Ordinal);
        StringAssert.Contains(firstHtml, "First place", StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(second));
        string secondHtml = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        StringAssert.Contains(secondHtml, "Second place", StringComparison.Ordinal);
        Assert.IsFalse(secondHtml.Contains("First place", StringComparison.Ordinal));
    }

    private static GeoJsonFeature Feature(string id, string label) =>
        new(new GeoJsonPoint(9.732, 52.376), new GeoJsonProperties(id, label, label, "poi", "", null, null));
}
