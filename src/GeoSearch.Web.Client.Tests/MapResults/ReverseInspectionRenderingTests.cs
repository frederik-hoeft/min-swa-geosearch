using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Client.Tests.Search;
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
public sealed class ReverseInspectionRenderingTests
{
    [TestMethod]
    public async Task LateReverseCompletionCannotOverwriteNewerClickedPointAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SelectionProbe probe = new();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(probe);
        services.AddSingleton(new SearchApiClient(client));
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        HtmlRootComponent root = await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);

        Task first = Task.CompletedTask;
        Task second = Task.CompletedTask;
        GeoCoordinate pointA = new(52.37, 9.73);
        GeoCoordinate pointB = new(52.38, 9.74);
        await renderer.Dispatcher.InvokeAsync(() => { first = probe.Workspace.InspectMapPointAsync(pointA); });
        await renderer.Dispatcher.InvokeAsync(() => { second = probe.Workspace.InspectMapPointAsync(pointB); });
        Assert.AreEqual(2, handler.RequestCount);

        handler.Complete(1, EmptyCollection());
        await second;
        handler.Complete(0, FeatureCollection("old result"));
        await first;

        ExploreSearchState state = GetState(probe.Workspace);
        Assert.AreEqual(pointB, ((ReverseInspectionPanel)state.Panel).Point);
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        StringAssert.Contains(html, "MAP INSPECTION", StringComparison.Ordinal);
        StringAssert.Contains(html, "No known address or place", StringComparison.Ordinal);
        Assert.IsFalse(html.Contains("old result", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task LeavingInspectionRejectsItsPendingResponseAndRestoresResultsAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SelectionProbe probe = new();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(probe);
        services.AddSingleton(new SearchApiClient(client));
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        HtmlRootComponent root = await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("Hannover")
            .WithForwardResult("Hannover", new ReadySearch(ExploreSearchStateTests.Collection("existing place")));
        typeof(SearchWorkspace).GetField("_searchState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(probe.Workspace, committed);

        Task reverse = Task.CompletedTask;
        await renderer.Dispatcher.InvokeAsync(() => { reverse = probe.Workspace.InspectMapPointAsync(new GeoCoordinate(52.37, 9.73)); });
        Assert.AreEqual(1, handler.RequestCount);
        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(committed is { Panel: ForwardSearchPanel { Results: ReadySearch ready } }
            ? ready.Collection.Features[0] : throw new InvalidOperationException()));
        handler.Complete(0, FeatureCollection("outdated reverse"));
        await reverse;

        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        StringAssert.Contains(html, "PLACE DETAILS", StringComparison.Ordinal);
        StringAssert.Contains(html, "existing place", StringComparison.Ordinal);
        Assert.IsFalse(html.Contains("outdated reverse", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReselectingDirectPlaceMarkerDuringInspectionDoesNotRestoreUnrelatedMarkersAsync()
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

        GeoJsonFeatureCollection priorResults = ExploreSearchStateTests.Collection("from query A");
        GeoJsonFeature otherPlace = new(new GeoJsonPoint(9.73, 52.37),
            new GeoJsonProperties("osm:b", "B", "Place B", "poi", "", null, null));
        ExploreSearchState queryA = ExploreSearchState.Initial.CommitQuery("query A")
            .WithForwardResult("query A", new ReadySearch(priorResults));
        ExploreSearchState inspected = queryA.CommitPlace(otherPlace, showReturnResults: false)
            .InspectMapPoint(new GeoCoordinate(52.38, 9.74));
        typeof(SearchWorkspace).GetField("_searchState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(probe.Workspace, inspected);

        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.SelectMapFeatureAsync(otherPlace));
        ExploreSearchState after = GetState(probe.Workspace);
        Assert.IsInstanceOfType<PlaceSearchPanel>(after.Panel);
        Assert.IsFalse(((PlaceSearchPanel)after.Panel).ShowReturnResults);
        Assert.AreEqual(0, SearchMapState.FromPanel(after.Panel).Results.Count);
        Assert.AreSame(queryA.Panel, after.Back().Panel);
    }

    [TestMethod]
    public async Task FirstBackgroundClickDismissesAutocompleteAndSecondInspectsAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SelectionProbe probe = new();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton(probe);
        services.AddSingleton(new SearchApiClient(client));
        using ServiceProvider provider = services.BuildServiceProvider();
        await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        HtmlRootComponent root = await renderer.Dispatcher.InvokeAsync(renderer.RenderComponentAsync<SearchWorkspaceHarness>);

        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("query A")
            .WithForwardResult("query A", new ReadySearch(ExploreSearchStateTests.Collection("place A")));
        ExploreSearchState draft = committed.Edit("query B");
        typeof(SearchWorkspace).GetField("_searchState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(probe.Workspace, draft);

        GeoCoordinate point = new(52.38, 9.74);
        await renderer.Dispatcher.InvokeAsync(() => probe.Workspace.HandleMapBackgroundClickAsync(point));
        ExploreSearchState dismissed = GetState(probe.Workspace);
        Assert.AreSame(committed.Panel, dismissed.Panel);
        Assert.IsFalse(dismissed.IsEditing);
        Assert.AreEqual("query A", dismissed.DisplayText);
        Assert.AreEqual(1, SearchMapState.FromPanel(dismissed.Panel).Results.Count);
        Assert.AreEqual(0, handler.RequestCount);
        string dismissedHtml = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        StringAssert.Contains(dismissedHtml, "place A", StringComparison.Ordinal);
        Assert.IsFalse(dismissedHtml.Contains("map-search-suggestions", StringComparison.Ordinal));

        Task second = Task.CompletedTask;
        await renderer.Dispatcher.InvokeAsync(() => { second = probe.Workspace.HandleMapBackgroundClickAsync(point); });
        Assert.AreEqual(0, handler.RequestCount); // No reverse lookup until single-click confirmation.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        while (handler.RequestCount == 0)
        {
            await Task.Delay(20, timeout.Token);
        }

        Assert.AreEqual(1, handler.RequestCount);
        handler.Complete(0, EmptyCollection());
        await second;
        ExploreSearchState inspection = GetState(probe.Workspace);
        Assert.IsInstanceOfType<ReverseInspectionPanel>(inspection.Panel);
        Assert.AreSame(committed.Panel, ((ReverseInspectionPanel)inspection.Panel).ReturnTo);
        Assert.AreEqual(1, SearchMapState.FromPanel(inspection.Panel).Results.Count);

        await renderer.Dispatcher.InvokeAsync(probe.Workspace.DismissMapInspectionAsync);
        Assert.AreSame(committed.Panel, GetState(probe.Workspace).Panel);
    }

    private static ExploreSearchState GetState(SearchWorkspace workspace) =>
        (ExploreSearchState)typeof(SearchWorkspace).GetField("_searchState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(workspace)!;

    private static string EmptyCollection() => """{"type":"FeatureCollection","snapshot":"test","features":[]}""";

    private static string FeatureCollection(string name) => """
        {"type":"FeatureCollection","snapshot":"test","features":[
          {"type":"Feature","geometry":{"type":"Point","coordinates":[9.73,52.37]},
           "properties":{"id":"osm:other","name":"NAME","label":"NAME","type":"place","category":"","score":null,"distance_m":12.5}}
        ]}
        """.Replace("NAME", name, StringComparison.Ordinal);
}
