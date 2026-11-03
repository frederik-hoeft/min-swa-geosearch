using GeoSearch.Web.Server.Data.Search;
using GeoSearch.Web.Server.Search;
using GeoSearch.Web.Shared.GeoJson;
using Moq;

namespace GeoSearch.Web.Search.Tests;

[TestClass]
public sealed class GeoSearchServiceTests
{
    [TestMethod]
    public async Task SearchMapsRankedResultsToGeoJsonWithSnapshotAsync()
    {
        Mock<ISearchReadRepository> repository = new(MockBehavior.Strict);
        repository.Setup(query => query.SearchAsync("Limmerstr 12 Hannover", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchReadResult("2026-10-28", [new SearchFeatureRow
            {
                Id = "address:123",
                Name = "Limmerstraße 12",
                Label = "Limmerstraße 12, Linden-Nord, Hannover",
                Type = "address",
                Category = "Adresse",
                Score = 0.73f,
                Longitude = 9.71497,
                Latitude = 52.36677,
                SnapshotId = "2026-10-28"
            }]));

        GeoSearchService service = new(repository.Object);
        GeoJsonFeatureCollection result = await service.SearchAsync("Limmerstr 12 Hannover", 5, CancellationToken.None);

        Assert.AreEqual("2026-10-28", result.Snapshot);
        Assert.HasCount(1, result.Features);
        Assert.AreEqual(9.71497, result.Features[0].Geometry.Coordinates[0]);
        Assert.AreEqual(52.36677, result.Features[0].Geometry.Coordinates[1]);
        Assert.AreEqual(0.73f, result.Features[0].Properties.Score);
        Assert.IsNull(result.Features[0].Properties.DistanceM);
        repository.VerifyAll();
    }

    [TestMethod]
    public async Task NearbyPreservesDistanceAndForwardsCategoryAndRadiusAsync()
    {
        Mock<ISearchReadRepository> repository = new(MockBehavior.Strict);
        repository.Setup(query => query.NearbyAsync(52.3759, 9.7320, 500, "Apotheke", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchReadResult("2026-10-28", [new SearchFeatureRow
            {
                Id = "pois:123",
                Name = "Georg Apotheke",
                Label = "Georg Apotheke, Hannover",
                Type = "poi",
                Category = "Apotheke",
                Longitude = 9.732,
                Latitude = 52.376,
                DistanceM = 192,
                SnapshotId = "2026-10-28"
            }]));

        GeoSearchService service = new(repository.Object);
        GeoJsonFeatureCollection result = await service.NearbyAsync(52.3759, 9.7320, 500, "Apotheke", 20, CancellationToken.None);

        Assert.AreEqual(192d, result.Features[0].Properties.DistanceM);
        Assert.IsNull(result.Features[0].Properties.Score);
        repository.VerifyAll();
    }

    [TestMethod]
    public async Task EmptyResultsKeepRepositorySnapshotAsync()
    {
        Mock<ISearchReadRepository> repository = new(MockBehavior.Strict);
        repository.Setup(query => query.ReverseAsync(52, 9, 250, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchReadResult("2026-10-28", []));

        GeoSearchService service = new(repository.Object);
        GeoJsonFeatureCollection result = await service.ReverseAsync(52, 9, 250, CancellationToken.None);

        Assert.AreEqual("2026-10-28", result.Snapshot);
        Assert.IsEmpty(result.Features);
        repository.VerifyAll();
    }
}
