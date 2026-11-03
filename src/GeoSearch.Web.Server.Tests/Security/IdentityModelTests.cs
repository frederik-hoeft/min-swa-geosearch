using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class IdentityModelTests
{
    [TestMethod]
    public void IdentityMigrationMatchesConfiguredModel()
    {
        Type modelLoaderType = typeof(IdentityDbContext).Assembly.GetType(
            "GeoSearch.Web.Server.Data.GeoSearchModelLoader", throwOnError: true)!;
        IModelLoader modelLoader = (IModelLoader)Activator.CreateInstance(modelLoaderType, nonPublic: true)!;
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;

        using IdentityDbContext dbContext = new(options, modelLoader);
        string[] migrations = [.. dbContext.Database.GetMigrations()];

        Assert.HasCount(2, migrations);
        Assert.Contains("InitialIdentity", migrations[0], StringComparison.Ordinal);
        Assert.Contains("AddRefreshTokenSessions", migrations[1], StringComparison.Ordinal);
        Assert.IsFalse(dbContext.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public void IdentityModelHasSeparateSchemaAndRegisteredUser()
    {
        Type modelLoaderType = typeof(IdentityDbContext).Assembly.GetType(
            "GeoSearch.Web.Server.Data.GeoSearchModelLoader", throwOnError: true)!;
        IModelLoader modelLoader = (IModelLoader)Activator.CreateInstance(modelLoaderType, nonPublic: true)!;
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;

        using IdentityDbContext dbContext = new(options, modelLoader);
        IEntityType? userModel = dbContext.Model.FindEntityType(typeof(GeoSearchUser));

        Assert.IsNotNull(userModel);
        Assert.AreEqual("app_auth", userModel.GetSchema());
        Assert.AreEqual("AspNetUsers", userModel.GetTableName());
        Assert.IsNull(dbContext.Model.FindEntityType(typeof(SearchFeatureRow)));
    }
}
