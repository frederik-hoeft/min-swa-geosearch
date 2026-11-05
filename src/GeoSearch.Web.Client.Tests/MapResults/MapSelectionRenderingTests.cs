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
        probe.Workspace.SearchState = committed;

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

    [TestMethod]
    public async Task ReselectingDirectAutocompletePinKeepsPriorQueryMarkersHiddenUntilBackAsync()
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

        await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);
        Assert.IsNotNull(probe.Workspace);

        GeoJsonFeature firstA = Feature("osm:a1", "A1");
        GeoJsonFeature secondA = Feature("osm:a2", "A2");
        GeoJsonFeature placeB = Feature("osm:b1", "B1");
        GeoJsonFeatureCollection resultsA = new("test", [firstA, secondA]);
        ExploreSearchState queryA = ExploreSearchState.Initial.CommitQuery("query A")
            .WithForwardResult("query A", new ReadySearch(resultsA));
        ExploreSearchState directB = queryA.Edit("query B")
            .CommitPlace(placeB, showReturnResults: false);
        probe.Workspace.SearchState = directB;

        // The same pin's event takes the normal map-selection path. It must not
        // make the unrelated prior query A markers reappear.
        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(placeB));
        ExploreSearchState afterClick = probe.Workspace.SearchState;
        SearchMapState map = SearchMapState.FromPanel(afterClick.Panel);
        Assert.AreSame(directB.Panel, afterClick.Panel);
        Assert.HasCount(0, map.Results);
        Assert.AreSame(placeB, map.SelectedFeature);
        Assert.AreSame(queryA.Panel, afterClick.Back().Panel);
        Assert.AreSame(resultsA.Features, SearchMapState.FromPanel(afterClick.Back().Panel).Results);

        // Even if a draft was open, reselecting the pin dismisses the draft
        // while retaining the same place inspection and return target.
        probe.Workspace.SearchState = directB.Edit("unfinished");
        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(placeB));
        ExploreSearchState afterDraft = probe.Workspace.SearchState;
        Assert.IsFalse(afterDraft.IsEditing);
        Assert.AreEqual(placeB.Properties.Label, afterDraft.DisplayText);
        Assert.AreSame(directB.Panel, afterDraft.Panel);
    }

    [TestMethod]
    public async Task ExternalMapCallbacksAfterDisposalDoNotRenderOrStartRequestsAsync()
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
        await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);

        await renderer.Dispatcher.InvokeAsync(probe.Workspace.Dispose);
        await probe.Workspace.DismissDraftAsync();
        await probe.Workspace.SelectMapFeatureAsync(Feature("osm:1", "Place"));
        await probe.Workspace.InspectMapPointAsync(new GeoCoordinate(52.37, 9.73));
        await probe.Workspace.DismissMapInspectionAsync();
        Assert.AreSame(ExploreSearchState.Initial, probe.Workspace.SearchState);
    }

    private static GeoJsonFeature Feature(string id, string label) =>
        new(new GeoJsonPoint(9.732, 52.376), new GeoJsonProperties(id, label, label, "poi", "", null, null));
}
