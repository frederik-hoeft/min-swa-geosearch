using GeoSearch.Web.Client.Services.Search;
using System.Net;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class CategoryCatalogSessionTests
{
    [TestMethod]
    public async Task CategoriesAreMatchedLocallyWithoutFurtherSearchRequestsAsync()
    {
        using CategoryHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using CategoryCatalogSession catalog = new(new SearchApiClient(client));
        int renders = 0;
        await catalog.EnsureLoadedAsync(() => { ++renders; return Task.CompletedTask; });
        Assert.IsTrue(catalog.HasLoaded);
        Assert.AreEqual(2, catalog.Categories.Count);
        Assert.AreEqual("Café", catalog.Match("CAFE").Single());
        Assert.AreEqual("Apotheke", catalog.Match("apoth").Single());
        Assert.AreEqual(1, renders);
        Assert.AreEqual(1, handler.RequestCount);
        await catalog.EnsureLoadedAsync(() => Task.CompletedTask);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task CatalogFailureCanRetryAsync()
    {
        using CategoryHandler handler = new() { FailNext = true };
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using CategoryCatalogSession catalog = new(new SearchApiClient(client));
        await catalog.EnsureLoadedAsync(() => Task.CompletedTask);
        Assert.IsNotNull(catalog.Error);
        Assert.IsFalse(catalog.HasLoaded);
        await catalog.RetryAsync(() => Task.CompletedTask);
        Assert.IsTrue(catalog.HasLoaded);
        Assert.IsNull(catalog.Error);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task TimedOutRequestIsRecoverableAndNotifiesForRetryAsync()
    {
        using CategoryHandler handler = new() { TimeoutNext = true };
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using CategoryCatalogSession catalog = new(new SearchApiClient(client));
        int renders = 0;
        await catalog.EnsureLoadedAsync(() => { ++renders; return Task.CompletedTask; });

        Assert.IsFalse(catalog.HasLoaded);
        Assert.IsNotNull(catalog.Error);
        Assert.AreEqual(1, renders);
        await catalog.RetryAsync(() => { ++renders; return Task.CompletedTask; });
        Assert.IsTrue(catalog.HasLoaded);
        Assert.IsNull(catalog.Error);
        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreEqual(2, renders);
    }

    private sealed class CategoryHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool FailNext { get; set; }
        public bool TimeoutNext { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (TimeoutNext)
            {
                TimeoutNext = false;
                throw new TaskCanceledException("The request timed out.");
            }
            bool failing = FailNext;
            FailNext = false;
#pragma warning disable CA2000
            return Task.FromResult(new HttpResponseMessage(failing ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent("[\"Café\",\"Apotheke\",\"Café\"]", Encoding.UTF8, "application/json")
            });
#pragma warning restore CA2000
        }
    }
}
