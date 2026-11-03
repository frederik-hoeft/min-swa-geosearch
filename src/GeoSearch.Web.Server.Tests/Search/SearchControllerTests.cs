using GeoSearch.Web.Server.Api.Search;
using GeoSearch.Web.Server.Search;
using GeoSearch.Web.Shared.Auth;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GeoSearch.Web.Server.Tests.Search;

[TestClass]
public sealed class SearchControllerTests
{
    [TestMethod]
    public async Task SearchRejectsMissingBlankAndOverlongQueryAsync()
    {
        SearchController controller = new(new Mock<IGeoSearchService>(MockBehavior.Strict).Object);

        foreach (string? query in new string?[] { null, "", "  ", new('x', 201) })
        {
            ActionResult<GeoJsonFeatureCollection> result = await controller.SearchAsync(query, null, CancellationToken.None);
            BadRequestObjectResult error = Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
            Assert.IsInstanceOfType<ApiError>(error.Value);
        }
    }

    [TestMethod]
    public async Task SearchUsesDefaultLimitAndReturnsResultAsync()
    {
        Mock<IGeoSearchService> service = new(MockBehavior.Strict);
        GeoJsonFeatureCollection response = new("2026-10-28", []);
        service.Setup(query => query.SearchAsync("Rathaus Linden", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        SearchController controller = new(service.Object);

        ActionResult<GeoJsonFeatureCollection> result = await controller.SearchAsync("  Rathaus Linden  ", null, CancellationToken.None);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(response, ok.Value);
        service.VerifyAll();
    }

    [TestMethod]
    public async Task ReverseRejectsOutOfRangePositionAndRadiusAsync()
    {
        SearchController controller = new(new Mock<IGeoSearchService>(MockBehavior.Strict).Object);
        foreach ((double? lat, double? lon, double? radius) in new[]
        {
            ((double?)null, (double?)9, (double?)250),
            ((double?)90.01, (double?)9, (double?)250),
            ((double?)52, (double?)180.01, (double?)250),
            ((double?)52, (double?)9, (double?)0),
            ((double?)52, (double?)9, (double?)1000.01),
            ((double?)double.NaN, (double?)9, (double?)250)
        })
        {
            ActionResult<GeoJsonFeatureCollection> result = await controller.ReverseAsync(lat, lon, radius, CancellationToken.None);
            Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        }
    }

    [TestMethod]
    public async Task NearbyRejectsOutOfRangeLimitsRadiusAndCategoryAsync()
    {
        SearchController controller = new(new Mock<IGeoSearchService>(MockBehavior.Strict).Object);

        ActionResult<GeoJsonFeatureCollection> overRadius = await controller.NearbyAsync(52, 9, 2001, null, null, CancellationToken.None);
        ActionResult<GeoJsonFeatureCollection> overLimit = await controller.NearbyAsync(52, 9, 500, null, 21, CancellationToken.None);
        ActionResult<GeoJsonFeatureCollection> blankCategory = await controller.NearbyAsync(52, 9, 500, "  ", null, CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(overRadius.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(overLimit.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(blankCategory.Result);
    }

    [TestMethod]
    public async Task CategoriesReturnsTheRepositoryValuesAsync()
    {
        Mock<IGeoSearchService> service = new(MockBehavior.Strict);
        IReadOnlyList<string> categories = ["Apotheke", "Bibliothek"];
        service.Setup(query => query.CategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        SearchController controller = new(service.Object);

        ActionResult<IReadOnlyList<string>> result = await controller.CategoriesAsync(CancellationToken.None);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(categories, ok.Value);
        service.VerifyAll();
    }
}
