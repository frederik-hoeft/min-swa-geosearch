using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.RefreshTokens;
using GeoSearch.Web.Server.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Text;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Built-in request-start diagnostics can include latitude/longitude query parameters.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

string identityConnectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity is required. See docs/development.md.");

JwtOptions jwt = JwtOptions.FromConfiguration(builder.Configuration.GetSection(JwtOptions.SECTION));

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<JwtTokenIssuer>();
builder.Services.AddScoped<IAuthenticationFlowService, AuthenticationFlowService>();
builder.Services.AddSingleton<IAuthenticationTimingService, PasswordHashAuthenticationTimingService>();
builder.Services.AddScoped<IRefreshTokenDataAccess, RefreshTokenDataAccess>();
builder.Services.AddOptions<RefreshTokenOptions>()
    .Bind(builder.Configuration.GetSection(RefreshTokenOptions.SECTION_NAME))
    .Validate(options => options.Lifetime > TimeSpan.Zero && options.Lifetime <= TimeSpan.FromDays(90)
        && options.CookieName.StartsWith("__Host-", StringComparison.Ordinal),
        "Refresh token lifetime must be 1-90 days and the cookie must use the __Host- prefix.")
    .ValidateOnStart();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-geosearch-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddSingleton<IModelLoader, GeoSearchModelLoader>();
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(identityConnectionString));

// The imported database is queried read-only. It is never migrated by EF.
string searchConnectionString = builder.Configuration.GetConnectionString("Search")
    ?? throw new InvalidOperationException("ConnectionStrings:Search is required. See docs/development.md.");
builder.Services.AddDbContext<SearchReadDbContext>(options => options.UseNpgsql(searchConnectionString));

builder.Services.AddIdentityCore<GeoSearchUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15),
            NameClaimType = System.Security.Claims.ClaimTypes.Email
        };
    });
builder.Services.AddAuthorization();
// Refresh and logout are cookie-authenticated and require a request-token header.
// Authentication itself remains stateless JWT bearer for normal API requests.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransactionManagement<IdentityDbContext>(options =>
    options.UseIsolationLevel(IsolationLevel.ReadCommitted));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

if (builder.Environment.IsDevelopment())
{
    string clientOrigin = builder.Configuration["Cors:ClientOrigin"] ?? "http://localhost:5200";
    builder.Services.AddCors(options => options.AddPolicy("DevClient", policy =>
        policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Only Identity data is app-owned. The instructor's osm/search schemas are never migrated by EF.
await IdentityBootstrap.InitializeAsync(app);
await app.RunAsync();

// Expose the conventional entry point to integration test hosts in a later iteration.
public partial class Program
{
}
