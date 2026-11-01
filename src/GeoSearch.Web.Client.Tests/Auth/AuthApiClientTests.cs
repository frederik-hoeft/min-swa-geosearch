using GeoSearch.Web.Client.Api;
using GeoSearch.Web.Shared.Auth;
using System.Net;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Auth;

[TestClass]
public sealed class AuthApiClientTests
{
    [TestMethod]
    public async Task RefreshUsesAntiforgeryHeaderAndDoesNotExposeRefreshTokenAsync()
    {
        List<(string Path, HttpMethod Method, string? Csrf)> calls = [];
        using StubHandler handler = new(request =>
        {
            calls.Add((request.RequestUri!.AbsolutePath, request.Method,
                request.Headers.TryGetValues("X-CSRF-TOKEN", out IEnumerable<string>? headers) ? headers.Single() : null));
            string body = calls.Count switch
            {
                1 => "{\"requestToken\":\"csrf-token\"}",
                2 => "{\"accessToken\":\"jwt\",\"expiresUtc\":\"2035-01-01T00:00:00Z\",\"email\":\"test@example.test\"}",
                _ => throw new InvalidOperationException("Unexpected request count."),
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });
        using HttpClient http = new(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5100") };
        AuthApiClient api = new(http);

        LoginResponse? response = await api.TryRefreshAsync();

        Assert.IsNotNull(response);
        Assert.AreEqual("jwt", response.AccessToken);
        Assert.HasCount(2, calls);
        Assert.AreEqual(("/api/auth/antiforgery", HttpMethod.Get, (string?)null), calls[0]);
        Assert.AreEqual(("/api/auth/refresh", HttpMethod.Post, (string?)"csrf-token"), calls[1]);
    }

    [TestMethod]
    public async Task RefreshReturnsNullOnUnauthorizedAfterAntiforgeryExchangeAsync()
    {
        int count = 0;
        using StubHandler handler = new(_ =>
        {
            count++;
            return count == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"requestToken\":\"csrf-token\"}", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        using HttpClient http = new(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5100") };
        AuthApiClient api = new(http);

        LoginResponse? result = await api.TryRefreshAsync();

        Assert.IsNull(result);
        Assert.AreEqual(2, count);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
