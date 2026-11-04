using GeoSearch.Web.Client.UI.Components;
using Microsoft.JSInterop;
using System.Reflection;

namespace GeoSearch.Web.Client.Tests.UI;

[TestClass]
public sealed class MapViewerInitializationTests
{
    [TestMethod]
    public async Task DisposalDuringModuleImportReleasesLateModuleAsync()
    {
        FakeJsRuntime runtime = new();
        await using TestMapViewer viewer = CreateViewer(runtime);

        Task initialization = viewer.StartAsync();
        await runtime.ImportRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await viewer.DisposeAsync();

        await using FakeModule module = new();
        runtime.ImportCompleted.SetResult(module);
        await initialization;

        Assert.AreEqual(1, module.DisposeCalls);
        Assert.AreEqual(0, module.ProbeCalls);
    }

    [TestMethod]
    public async Task DisposalDuringWebGlProbeReleasesModuleAsync()
    {
        FakeJsRuntime runtime = new();
        await using FakeModule module = new();
        runtime.ImportCompleted.SetResult(module);
        await using TestMapViewer viewer = CreateViewer(runtime);

        Task initialization = viewer.StartAsync();
        await module.ProbeRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await viewer.DisposeAsync();

        module.ProbeCompleted.SetResult(true);
        await initialization;

        Assert.AreEqual(1, module.ProbeCalls);
        Assert.AreEqual(1, module.DisposeCalls);
    }

    private static TestMapViewer CreateViewer(IJSRuntime runtime)
    {
        // The caller owns the viewer and disposes it.
#pragma warning disable CA2000
        TestMapViewer viewer = new();
#pragma warning restore CA2000
        typeof(MapViewer).GetProperty("JsRuntime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewer, runtime);
        typeof(MapViewer).GetField("_styleChecked", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewer, true);
        return viewer;
    }

    private sealed class TestMapViewer : MapViewer
    {
        public Task StartAsync() => OnAfterRenderAsync(firstRender: true);
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public TaskCompletionSource<bool> ImportRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IJSObjectReference> ImportCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.AreEqual("import", identifier);
            ImportRequested.TrySetResult(true);
            return new ValueTask<TValue>(AwaitImportAsync<TValue>());
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        private async Task<TValue> AwaitImportAsync<TValue>() => (TValue)await ImportCompleted.Task;
    }

    private sealed class FakeModule : IJSObjectReference
    {
        public TaskCompletionSource<bool> ProbeRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ProbeCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProbeCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.AreEqual("supportsWebGl2", identifier);
            ProbeCalls++;
            ProbeRequested.TrySetResult(true);
            return new ValueTask<TValue>(AwaitProbeAsync<TValue>());
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        private async Task<TValue> AwaitProbeAsync<TValue>() => (TValue)(object)await ProbeCompleted.Task;
    }
}
