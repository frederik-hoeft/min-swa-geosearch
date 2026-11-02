using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wkg.EntityFrameworkCore.Configuration;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.EntityNamingPolicies;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.PropertyMappingPolicies;
using Wkg.EntityFrameworkCore.Extensions;

namespace GeoSearch.Web.Server.Data;

/// <summary>
/// Identity-only EF model. Uses a separate, application-owned PostgreSQL database.
/// The default schema prevents collisions if additional application entities are introduced.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IModelLoader modelLoader)
    : IdentityDbContext<GeoSearchUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("app_auth");
        builder.LoadModels(modelLoader, options => options
            .ConfigurePolicies(policies => policies
                .AddPolicy<EntityNaming>(policy => policy.RequireExplicit())
                .AddPolicy<PropertyMapping>(policy => policy.RequireExplicit())));
    }
}
