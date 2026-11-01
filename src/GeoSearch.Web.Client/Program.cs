using GeoSearch.Web.Client.Api;
using GeoSearch.Web.Client.Auth;
using GeoSearch.Web.Client.UI;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BrowserAuthCoordinator>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<SessionAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services =>
    services.GetRequiredService<SessionAuthenticationStateProvider>());

string apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
builder.Services.AddScoped<AuthApiClient>();

await builder.Build().RunAsync();
