using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using System.Net;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class NearbyWorkspaceWorkflowTests
{
    [TestMethod]
    public async Task CategoryDraftThenFindCapturesMapCenterOnceAndSearchThisAreaRequiresManualMovementAsync()
    {
        using NearbyHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://example.test/") };
        GeoCoordinate mapCenter = new(52.37, 9.73);
        List<SearchMapState> published = [];
        using SearchWorkspaceWorkflow workspace = CreateWorkflow(http, () => Task.FromResult<GeoCoordinate?>(mapCenter),
            state => { published.Add(state); return Task.CompletedTask; });
        await workspace.OpenNearbyAsync("Apotheke");
        Assert.IsInstanceOfType<NearbySearchPanel>(workspace.State.Panel);
        Assert.AreEqual(0, handler.NearbyCount);

        Task pending = workspace.FindNearbyAsync();
        Assert.AreEqual(1, handler.NearbyCount);
        NearbySearchPanel committed = (NearbySearchPanel)workspace.State.Panel;
        Assert.AreEqual(mapCenter, committed.Applied!.Origin);
        Assert.AreEqual(500, committed.Applied.Criteria.RadiusMeters);
        Assert.AreEqual("Apotheke", committed.Applied.Criteria.Category);
        Assert.IsFalse(workspace.CanSearchThisArea);
        mapCenter = new GeoCoordinate(52.4, 9.8);
        workspace.SetMapCenter(mapCenter);
        Assert.IsTrue(workspace.CanSearchThisArea);
        Assert.AreEqual(1, handler.NearbyCount); // Panning never auto-queries.

        handler.Complete(0, Empty());
        await pending;
        Assert.IsInstanceOfType<ReadySearch>(((NearbySearchPanel)workspace.State.Panel).Results);
        Task replacement = workspace.SearchThisAreaAsync();
        Assert.AreEqual(2, handler.NearbyCount);
        Assert.AreEqual(mapCenter, ((NearbySearchPanel)workspace.State.Panel).Applied!.Origin);
        Assert.AreEqual(500, ((NearbySearchPanel)workspace.State.Panel).Applied!.Criteria.RadiusMeters);
        handler.Complete(1, Empty());
        await replacement;
        Assert.AreEqual(mapCenter, published[^1].Nearby!.Origin);
    }

    [TestMethod]
    public async Task MissingDeviceFixDoesNotSubmitOrRequestPermissionAsync()
    {
        using NearbyHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://example.test/") };
        int locationRequests = 0;
        using SearchWorkspaceWorkflow workspace = new(new SearchApiClient(http),
            () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask,
            operation => operation(), () => Task.FromResult<GeoCoordinate?>(new GeoCoordinate(52.3, 9.7)),
            _ => { ++locationRequests; return Task.FromResult(new DeviceLocationResult(null, "Denied")); });
        await workspace.OpenNearbyAsync(null);
        await workspace.EditNearbyAsync(new NearbyCriteria(OriginKind: NearbyOriginKind.DeviceFix));
        await workspace.FindNearbyAsync();
        Assert.AreEqual(0, locationRequests);
        Assert.AreEqual(0, handler.NearbyCount);
        StringAssert.Contains(workspace.NearbyError!, "Locate your device", StringComparison.Ordinal);
        Assert.IsTrue(((NearbySearchPanel)workspace.State.Panel).Editing);
    }

    [TestMethod]
    public async Task LateLocationFixCannotReplaceNewerInspectionOrAppliedDiscoveryAsync()
    {
        using NearbyHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://example.test/") };
        TaskCompletionSource<DeviceLocationResult> fix = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using SearchWorkspaceWorkflow workspace = new(new SearchApiClient(http),
            () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask,
            operation => operation(), () => Task.FromResult<GeoCoordinate?>(new GeoCoordinate(52.3, 9.7)),
            _ => fix.Task);
        Task locating = workspace.LocateMeAsync();
        workspace.SetMapCenter(new GeoCoordinate(52.35, 9.75));
        fix.SetResult(new DeviceLocationResult(new DeviceLocationFix(new GeoCoordinate(52.38, 9.74), 40), null));
        await locating;
        Assert.IsNotNull(workspace.Device);
        Assert.IsInstanceOfType<IdleSearchPanel>(workspace.State.Panel);
        Assert.AreEqual(0, handler.ReverseCount);
    }

    private static SearchWorkspaceWorkflow CreateWorkflow(HttpClient http, Func<Task<GeoCoordinate?>> mapCenter,
        Func<SearchMapState, Task> mapChanged) => new(new SearchApiClient(http),
        () => Task.CompletedTask, () => Task.CompletedTask, mapChanged,
        operation => operation(), mapCenter);

    private static string Empty() => """{"type":"FeatureCollection","snapshot":"test","features":[]}""";

    private sealed class NearbyHandler : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _requests = [];
        public int NearbyCount => _requests.Count;
        public int ReverseCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/categories", StringComparison.Ordinal))
            {
#pragma warning disable CA2000
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[\"Apotheke\"]", Encoding.UTF8, "application/json")
                });
#pragma warning restore CA2000
            }
            if (path.EndsWith("/reverse", StringComparison.Ordinal))
            {
                ReverseCount++;
            }

            TaskCompletionSource<HttpResponseMessage> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(result);
            // Ignore cancellation deliberately to exercise intent/revision guards.
            return result.Task;
        }

        public void Complete(int index, string json)
        {
#pragma warning disable CA2000
            _requests[index].SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
#pragma warning restore CA2000
        }
    }
}
