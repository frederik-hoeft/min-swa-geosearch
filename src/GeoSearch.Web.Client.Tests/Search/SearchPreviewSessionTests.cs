using GeoSearch.Web.Client.Services.Search;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class SearchPreviewSessionTests
{
    [TestMethod]
    public async Task SupersededDraftDoesNotPublishPreviewAndCommitCanReuseSuccessAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession queries = new(new SearchApiClient(client));
        TaskCompletionSource debounce = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using SearchPreviewSession preview = new(queries, token => debounce.Task.WaitAsync(token));
        TaskCompletionSource updated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int refreshes = 0;
        Task NotifyAsync()
        {
            ++refreshes;
            updated.TrySetResult();
            return Task.CompletedTask;
        }

        preview.Edit("First", NotifyAsync);
        preview.Edit("Second", NotifyAsync);
        debounce.TrySetResult();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        while (handler.RequestCount == 0)
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.AreEqual("/api/search?q=Second&limit=10", handler.LastRequest?.RequestUri?.PathAndQuery);
        handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        await updated.Task.WaitAsync(timeout.Token);
        Assert.AreEqual(1, refreshes);
        Assert.IsNull(preview.GetSuccessfulPreview("First"));
        SearchSuccess? success = preview.GetSuccessfulPreview("Second");
        Assert.IsNotNull(success);
        Assert.HasCount(1, preview.GetSuggestions("Second"));

        preview.CancelPending();
        Assert.IsNotNull(preview.GetSuccessfulPreview("Second"));
        Assert.IsInstanceOfType<SearchSuccess>(await queries.FetchAsync("Second"));
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public void ValidationHintsAndClearingDoNotRetainStalePreview()
    {
        using HttpClient client = new() { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession queries = new(new SearchApiClient(client));
        using SearchPreviewSession preview = new(queries);
        Assert.IsEmpty(preview.GetSuggestions("ab"));
        StringAssert.Contains(preview.GetInputHint("ab")!, "at least 3", StringComparison.Ordinal);
        StringAssert.Contains(preview.GetInputHint(new string('a', 201))!, "at most 200", StringComparison.Ordinal);
        preview.Edit("valid", () => Task.CompletedTask);
        StringAssert.Contains(preview.GetInputHint("valid")!, "Finding suggestions", StringComparison.Ordinal);
        preview.Clear();
        Assert.IsNull(preview.GetInputHint("valid"));
        Assert.IsNull(preview.GetSuccessfulPreview("valid"));
    }
}
