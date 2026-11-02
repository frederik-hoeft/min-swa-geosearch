using GeoSearch.Web.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GeoSearch.Web.Server.Security;

/// <summary>Initializes the independent Identity store and optionally creates one development account.</summary>
internal static class IdentityBootstrap
{
    internal static async Task InitializeAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CancellationToken cancellationToken = app.Lifetime.ApplicationStopping;
        // The application owns only the Identity database; imported GIS schemas are external.
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (!app.Environment.IsDevelopment())
        {
            return;
        }
        string? email = app.Configuration["Bootstrap:Email"];
        string? password = app.Configuration["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Both Bootstrap:Email and Bootstrap:Password must be supplied.");
        }

        UserManager<GeoSearchUser> users = scope.ServiceProvider.GetRequiredService<UserManager<GeoSearchUser>>();
        GeoSearchUser? existingUser = await users.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return;
        }
        GeoSearchUser newUser = new() { UserName = email, Email = email, EmailConfirmed = true };
        IdentityResult created = await users.CreateAsync(newUser, password);
        if (!created.Succeeded)
        {
            // Do not log credentials or details capable of containing supplied secrets.
            throw new InvalidOperationException("Development account provisioning failed. Check password policy.");
        }
    }
}
