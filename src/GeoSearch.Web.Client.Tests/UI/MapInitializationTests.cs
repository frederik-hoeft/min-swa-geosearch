using System.Reflection;
using GeoSearch.Web.Client.UI.Pages;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Tests.UI;

[TestClass]
public sealed class MapInitializationTests
{
    [TestMethod]
    public async Task DisposalDuringModuleImportReleasesLateModuleAsync()
    {
        FakeJsRuntime runtime = new();
        await using TestMap page = CreatePage(runtime);

        Task initialization = page.StartAsync();
        await runtime.ImportRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await page.DisposeAsync();

        await using FakeModule module = new();
        runtime.ImportCompleted.SetResult(module);
        await initialization;

        Assert.AreEqual(1, module.DisposeCalls);
        Assert.AreEqual(0, module.ObserveCalls);
    }

    [TestMethod]
    public async Task DisposalDuringObserverCreationReleasesAllLateResourcesAsync()
    {
        FakeJsRuntime runtime = new();
        await using FakeModule module = new();
        runtime.ImportCompleted.SetResult(module);
        await using TestMap page = CreatePage(runtime);

        Task initialization = page.StartAsync();
        await module.ObserveRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await page.DisposeAsync();

        await using FakeObserver observer = new();
        module.ObserverCompleted.SetResult(observer);
        await initialization;

        Assert.AreEqual(1, observer.StopCalls);
        Assert.AreEqual(1, observer.DisposeCalls);
        Assert.AreEqual(1, module.DisposeCalls);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = module.CapturedReference!.Value);
    }

    [TestMethod]
    public async Task SuccessfulInitializationTransfersResourcesToComponentAsync()
    {
        FakeJsRuntime runtime = new();
        await using FakeModule module = new();
        await using FakeObserver observer = new();
        runtime.ImportCompleted.SetResult(module);
        module.ObserverCompleted.SetResult(observer);
        await using TestMap page = CreatePage(runtime);

        await page.StartAsync();
        Assert.AreEqual(0, module.DisposeCalls);
        Assert.AreEqual(0, observer.StopCalls);

        await page.DisposeAsync();
        Assert.AreEqual(1, module.DisposeCalls);
        Assert.AreEqual(1, observer.StopCalls);
        Assert.AreEqual(1, observer.DisposeCalls);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = module.CapturedReference!.Value);
    }

    private static TestMap CreatePage(IJSRuntime runtime)
    {
        // The caller takes ownership and disposes the component.
#pragma warning disable CA2000
        TestMap page = new();
#pragma warning restore CA2000
        PropertyInfo property = typeof(Map).GetProperty("JsRuntime", BindingFlags.NonPublic | BindingFlags.Instance)!;
        property.SetValue(page, runtime);
        return page;
    }

    private sealed class TestMap : Map
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
        public TaskCompletionSource<bool> ObserveRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IJSObjectReference> ObserverCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DotNetObjectReference<Map>? CapturedReference { get; private set; }
        public int ObserveCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.AreEqual("observeSearchPanel", identifier);
            ObserveCalls++;
            CapturedReference = (DotNetObjectReference<Map>)args![1]!;
            ObserveRequested.TrySetResult(true);
            return new ValueTask<TValue>(AwaitObserverAsync<TValue>());
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        private async Task<TValue> AwaitObserverAsync<TValue>() => (TValue)await ObserverCompleted.Task;
    }

    private sealed class FakeObserver : IJSObjectReference
    {
        public int StopCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.AreEqual("dispose", identifier);
            StopCalls++;
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
