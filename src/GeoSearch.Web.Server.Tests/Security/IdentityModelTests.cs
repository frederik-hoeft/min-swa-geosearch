using GeoSearch.Web.Server.Data;
using Microsoft.EntityFrameworkCore;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class IdentityModelTests
{
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
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? userModel = dbContext.Model.FindEntityType(typeof(GeoSearchUser));

        Assert.IsNotNull(userModel);
        Assert.AreEqual("app_auth", userModel.GetSchema());
        Assert.AreEqual("AspNetUsers", userModel.GetTableName());
    }
}
