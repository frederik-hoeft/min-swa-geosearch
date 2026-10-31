using GeoSearch.Web.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GeoSearch.Web.Server.Security;

/// <summary>Initializes the independent Identity store and optionally creates one development account.</summary>
internal static class IdentityBootstrap
{
    internal static async Task InitializeAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope().ConfigureAwait(false);
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        CancellationToken cancellationToken = app.Lifetime.ApplicationStopping;
        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Bootstrap:EnsureCreated"))
        {
            // Only safe for a separate *empty* development database. Switch to migrations after the SDK audit.
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            IEnumerable<string> migrations = dbContext.Database.GetMigrations();
            bool hasMigrations = migrations.Any();
            if (!hasMigrations)
            {
                throw new InvalidOperationException("No Identity migrations exist. See docs/development.md.");
            }
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

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
        GeoSearchUser? existingUser = await users.FindByEmailAsync(email).ConfigureAwait(false);
        if (existingUser is not null)
        {
            return;
        }
        GeoSearchUser newUser = new() { UserName = email, Email = email, EmailConfirmed = true };
        IdentityResult created = await users.CreateAsync(newUser, password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            // Do not log credentials or details capable of containing supplied secrets.
            throw new InvalidOperationException("Development account provisioning failed. Check password policy.");
        }
    }
}
