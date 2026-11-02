using GeoSearch.Web.Server.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class AntiforgeryValidationMiddlewareTests
{
    [TestMethod]
    public async Task ProtectedEndpointRejectsMissingRequestTokenAsync()
    {
        using ServiceProvider services = CreateServices();
        RequestDelegate application = CreateApplication(services);
        DefaultHttpContext context = CreateContext(services, HttpMethods.Post, "/protected");

        await application(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.AreEqual("application/problem+json", context.Response.ContentType);
        string body = await ReadResponseAsync(context);
        using JsonDocument problem = JsonDocument.Parse(body);
        Assert.AreEqual("Antiforgery validation failed", problem.RootElement.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task ProtectedEndpointAcceptsIssuedRequestTokenAsync()
    {
        using ServiceProvider services = CreateServices();
        RequestDelegate application = CreateApplication(services);
        DefaultHttpContext issue = CreateContext(services, HttpMethods.Get, "/antiforgery");

        await application(issue);
        Assert.AreEqual(StatusCodes.Status200OK, issue.Response.StatusCode);
        string response = await ReadResponseAsync(issue);
        using JsonDocument tokens = JsonDocument.Parse(response);
        string requestToken = tokens.RootElement.GetProperty("token").GetString()!;
        string cookie = issue.Response.Headers.SetCookie.ToString().Split(';')[0];
        Assert.IsNotEmpty(cookie);

        DefaultHttpContext request = CreateContext(services, HttpMethods.Post, "/protected");
        request.Request.Headers.Cookie = cookie;
        request.Request.Headers["X-CSRF-TOKEN"] = requestToken;

        await application(request);

        Assert.AreEqual(StatusCodes.Status200OK, request.Response.StatusCode);
        Assert.AreEqual("ok", await ReadResponseAsync(request));
    }

    [TestMethod]
    public async Task ProtectedEndpointRejectsIncorrectRequestTokenAsync()
    {
        using ServiceProvider services = CreateServices();
        RequestDelegate application = CreateApplication(services);
        DefaultHttpContext issue = CreateContext(services, HttpMethods.Get, "/antiforgery");

        await application(issue);
        string cookie = issue.Response.Headers.SetCookie.ToString().Split(';')[0];
        DefaultHttpContext request = CreateContext(services, HttpMethods.Post, "/protected");
        request.Request.Headers.Cookie = cookie;
        request.Request.Headers["X-CSRF-TOKEN"] = "not-a-valid-antiforgery-token";

        await application(request);

        Assert.AreEqual(StatusCodes.Status400BadRequest, request.Response.StatusCode);
    }

    [TestMethod]
    public async Task UnmarkedEndpointDoesNotRequireRequestTokenAsync()
    {
        using ServiceProvider services = CreateServices();
        RequestDelegate application = CreateApplication(services);
        DefaultHttpContext context = CreateContext(services, HttpMethods.Post, "/unprotected");

        await application(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual("ok", await ReadResponseAsync(context));
    }

    private static ServiceProvider CreateServices()
    {
        ServiceCollection services = new();
        services.AddRouting();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-geosearch-csrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddSingleton<DiagnosticListener>(_ => new("GeoSearch.Antiforgery.Tests"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        return services.BuildServiceProvider();
    }

    private static RequestDelegate CreateApplication(IServiceProvider services)
    {
        ApplicationBuilder app = new(services);
        app.UseRouting();
        app.UseMiddleware<AntiforgeryValidationMiddleware>();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/antiforgery", async context =>
            {
                IAntiforgery antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
                await context.Response.WriteAsJsonAsync(new { token = tokens.RequestToken });
            });
            endpoints.MapPost("/protected", context => context.Response.WriteAsync("ok"))
                .WithMetadata(new RequireAntiforgeryValidationAttribute());
            endpoints.MapPost("/unprotected", context => context.Response.WriteAsync("ok"));
        });
        return app.Build();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string method, string path)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
