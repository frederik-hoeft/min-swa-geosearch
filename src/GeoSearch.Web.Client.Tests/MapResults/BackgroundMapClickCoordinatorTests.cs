using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class BackgroundMapClickCoordinatorTests
{
    private static readonly GeoCoordinate s_pointA = new(52.37, 9.73);
    private static readonly GeoCoordinate s_pointB = new(52.38, 9.74);

    [TestMethod]
    public async Task ConfirmedSingleClickInspectsExactlyOnceAsync()
    {
        TaskCompletionSource delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GeoCoordinate> inspected = [];
        using BackgroundMapClickCoordinator coordinator = new(
            point => { inspected.Add(point); return Task.CompletedTask; },
            token => delay.Task.WaitAsync(token));

        Task click = coordinator.ScheduleAsync(s_pointA);
        Assert.IsEmpty(inspected);

        delay.SetResult();
        await click;
        CollectionAssert.AreEqual(new[] { s_pointA }, inspected);
    }

    [TestMethod]
    public async Task DoubleClickZoomCancelsBothClickCandidatesAsync()
    {
        TaskCompletionSource delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GeoCoordinate> inspected = [];
        using BackgroundMapClickCoordinator coordinator = new(
            point => { inspected.Add(point); return Task.CompletedTask; },
            token => delay.Task.WaitAsync(token));

        Task firstClick = coordinator.ScheduleAsync(s_pointA);
        Task secondClick = coordinator.ScheduleAsync(s_pointA);
        coordinator.CancelPending(); // MapLibre's native dblclick notification.
        delay.SetResult();
        await Task.WhenAll(firstClick, secondClick);

        Assert.IsEmpty(inspected);
    }

    [TestMethod]
    public async Task ANewClickSupersedesThePreviousPendingPointAsync()
    {
        TaskCompletionSource delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GeoCoordinate> inspected = [];
        using BackgroundMapClickCoordinator coordinator = new(
            point => { inspected.Add(point); return Task.CompletedTask; },
            token => delay.Task.WaitAsync(token));

        Task first = coordinator.ScheduleAsync(s_pointA);
        Task second = coordinator.ScheduleAsync(s_pointB);
        delay.SetResult();
        await Task.WhenAll(first, second);

        CollectionAssert.AreEqual(new[] { s_pointB }, inspected);
    }

    [TestMethod]
    public async Task DisposingCancelsPendingInspectionAsync()
    {
        TaskCompletionSource delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using BackgroundMapClickCoordinator coordinator = new(
            _ => { ++calls; return Task.CompletedTask; },
            token => delay.Task.WaitAsync(token));

        Task click = coordinator.ScheduleAsync(s_pointA);
        coordinator.Dispose();
        delay.SetResult();
        await click;

        Assert.AreEqual(0, calls);
    }
}
