using GeoSearch.Web.Server.Data;
using Microsoft.EntityFrameworkCore;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class RefreshTokenModelTests
{
    [TestMethod]
    public void TokenModelUsesUniqueHashAndConcurrencyToken()
    {
        Type loaderType = typeof(IdentityDbContext).Assembly.GetType("GeoSearch.Web.Server.Data.GeoSearchModelLoader", true)!;
        IModelLoader loader = (IModelLoader)Activator.CreateInstance(loaderType, nonPublic: true)!;
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using IdentityDbContext db = new(options, loader);
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? token = db.Model.GetEntityTypes().SingleOrDefault(
            static entity => entity.ClrType.Name == "RefreshToken");

        Assert.IsNotNull(token);
        Assert.AreEqual("app_auth", token.GetSchema());
        Assert.AreEqual("RefreshTokens", token.GetTableName());
        Assert.IsTrue(token.FindProperty("ConcurrencyToken")!.IsConcurrencyToken);
        Assert.IsTrue(token.GetIndexes().Any(static index => index.IsUnique
            && index.Properties.Count == 1 && index.Properties[0].Name == "TokenHash"));
    }
}
