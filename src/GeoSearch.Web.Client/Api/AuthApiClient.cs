using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GeoSearch.Web.Client.Api;

/// <summary>Cookie-bearing authentication API; no tokens are persisted by the client.</summary>
public sealed class AuthApiClient(HttpClient httpClient)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "api/auth/login")
        {
            Content = JsonContent.Create(request),
        };
        message.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
    }

    public async Task<LoginResponse?> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = await CreateProtectedRequestAsync("api/auth/refresh", cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = await CreateProtectedRequestAsync("api/auth/logout", cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LoginResponse?> ChangePasswordAsync(ChangePasswordRequest change, string accessToken, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "api/auth/password")
        {
            Content = JsonContent.Create(change),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
    }

    public async Task<AccountResponse?> GetAccountAsync(string token, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateProtectedRequestAsync(string path, CancellationToken cancellationToken)
    {
        using HttpRequestMessage csrfRequest = new(HttpMethod.Get, "api/auth/antiforgery");
        csrfRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using HttpResponseMessage csrfResponse = await httpClient.SendAsync(csrfRequest, cancellationToken);
        csrfResponse.EnsureSuccessStatusCode();
        AntiforgeryResponse csrf = await csrfResponse.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Missing antiforgery response.");
        HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
