using System.Data;
using System.Text;
using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity is required. See docs/development.md.");

JwtOptions jwt = JwtOptions.FromConfiguration(builder.Configuration.GetSection(JwtOptions.SECTION));

builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<JwtTokenIssuer>();
builder.Services.AddSingleton<IModelLoader, GeoSearchModelLoader>();
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 16;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransactionManagement<IdentityDbContext>(options =>
    options.UseIsolationLevel(IsolationLevel.ReadCommitted));

builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter =>
{
    limiter.PermitLimit = 5;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

if (builder.Environment.IsDevelopment())
{
    string clientOrigin = builder.Configuration["Cors:ClientOrigin"] ?? "http://localhost:5200";
    builder.Services.AddCors(options => options.AddPolicy("DevClient", policy =>
        policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod()));
}

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
public partial class Program;
