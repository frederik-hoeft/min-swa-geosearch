using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.Search;
using GeoSearch.Web.Server.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Wkg.AspNetCore.Transactions.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Built-in request-start diagnostics can include latitude/longitude query parameters.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

// The imported database is queried read-only. It is never migrated by EF.
string searchConnectionString = builder.Configuration.GetConnectionString("Search")
    ?? throw new InvalidOperationException("ConnectionStrings:Search is required. See docs/development.md.");
builder.Services.AddSingleton<SearchReadModelLoader>();
builder.Services.AddDbContext<SearchReadDbContext>(options =>
    options.UseNpgsql(searchConnectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
builder.Services.AddScoped<ISearchReadRepository, SearchReadRepository>();
builder.Services.AddScoped<IGeoSearchService, GeoSearchService>();
builder.Services.AddTransactionManagement<SearchReadDbContext>(options =>
    options.UseIsolationLevel(IsolationLevel.ReadCommitted));

builder.Services.AddControllers();
// Preserve the lab's error envelope for malformed GIS query parameters.
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = _ =>
        new BadRequestObjectResult(new GeoSearch.Web.Shared.Api.ApiError("Invalid request parameters.")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (builder.Environment.IsDevelopment())
{
    string clientOrigin = builder.Configuration["Cors:ClientOrigin"] ?? "https://localhost:5200";
    builder.Services.AddCors(options => options.AddPolicy("DevClient", policy =>
        policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod()));
}

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevClient");
}
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
await app.RunAsync();
