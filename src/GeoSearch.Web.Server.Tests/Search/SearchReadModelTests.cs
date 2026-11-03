using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GeoSearch.Web.Server.Tests.Search;

[TestClass]
public sealed class SearchReadModelTests
{
    [TestMethod]
    public void SearchRowIsKeylessAndHasExplicitFunctionResultColumns()
    {
        DbContextOptions<SearchReadDbContext> options = new DbContextOptionsBuilder<SearchReadDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;

        using SearchReadDbContext context = new(options, new SearchReadModelLoader());
        IEntityType? entity = context.Model.FindEntityType(typeof(SearchFeatureRow));

        Assert.IsNotNull(entity);
        Assert.IsNull(entity.FindPrimaryKey());
        Assert.AreEqual("lon", entity.FindProperty(nameof(SearchFeatureRow.Longitude))?.GetColumnName());
        Assert.AreEqual("lat", entity.FindProperty(nameof(SearchFeatureRow.Latitude))?.GetColumnName());
        Assert.AreEqual("snapshot_id", entity.FindProperty(nameof(SearchFeatureRow.SnapshotId))?.GetColumnName());
        Assert.AreEqual("distance_m", entity.FindProperty(nameof(SearchFeatureRow.DistanceM))?.GetColumnName());
        Assert.IsNotNull(context.Model.FindEntityType(typeof(SearchIndexMetadataRow)));
        Assert.IsNotNull(context.Model.FindEntityType(typeof(OsmSnapshotRow)));
        Assert.IsNull(context.Model.FindEntityType(typeof(GeoSearchUser)));

        // The query roots must translate to database functions, not client-side invocation.
        Assert.Contains("search.api_search", context.SearchFeatures("Linden", 5).ToQueryString());
        Assert.Contains("search.api_reverse", context.ReverseFeatures(9.73, 52.37, 250).ToQueryString());
        Assert.Contains("search.api_nearby", context.NearbyFeatures(9.73, 52.37, 500, null, 20).ToQueryString());
    }

    [TestMethod]
    public void SearchContextRejectsSaveChanges()
    {
        DbContextOptions<SearchReadDbContext> options = new DbContextOptionsBuilder<SearchReadDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;

        using SearchReadDbContext context = new(options, new SearchReadModelLoader());
        Assert.ThrowsExactly<NotSupportedException>(() => context.SaveChanges());
    }
}
