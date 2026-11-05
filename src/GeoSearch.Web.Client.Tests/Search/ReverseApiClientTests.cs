using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using System.Globalization;
using System.Net;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class ReverseApiClientTests
{
    [TestMethod]
    public async Task ReverseRequestUsesNamedWgs84ParametersAndInvariantFormattingAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SearchApiClient search = new(client);
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Task<SearchFetchResult> request = search.ReverseAsync(new GeoCoordinate(52.3759, 9.7320), CancellationToken.None);
            Assert.AreEqual("/api/reverse?lat=52.3759&lon=9.732&radius=250", handler.LastRequest?.RequestUri?.PathAndQuery);
            handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
            SearchFetchResult result = await request;
            Assert.IsInstanceOfType<SearchSuccess>(result);
            Assert.AreEqual(0, ((SearchSuccess)result).Collection.Features.Count);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public async Task ReverseErrorIsARecoverableOutcomeAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SearchApiClient search = new(client);
        Task<SearchFetchResult> request = search.ReverseAsync(new GeoCoordinate(52.37, 9.73), CancellationToken.None);
        handler.Complete("""{"error":"Invalid point"}""", HttpStatusCode.BadRequest);

        Assert.AreEqual("Invalid point", ((SearchFailure)await request).Message);
    }

    [TestMethod]
    public async Task ReverseUsesDistanceFromClickedPointAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SearchApiClient search = new(client);
        Task<SearchFetchResult> request = search.ReverseAsync(new GeoCoordinate(52.3759, 9.732), CancellationToken.None);
        handler.Complete("""
            {"type":"FeatureCollection","snapshot":"test","features":[
                {"type":"Feature","geometry":{"type":"Point","coordinates":[9.731,52.376]},
                 "properties":{"id":"pois:3","name":"Test","label":"Test place","type":"poi",
                 "category":"Apotheke","score":null,"distance_m":38.5}}
            ]}
            """);

        Assert.AreEqual(38.5, ((SearchSuccess)await request).Collection.Features[0].Properties.DistanceM);
    }
}
