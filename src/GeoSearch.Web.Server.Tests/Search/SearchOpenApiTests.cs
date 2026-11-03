using GeoSearch.Web.Server.Api.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace GeoSearch.Web.Server.Tests.Search;

[TestClass]
public sealed class SearchOpenApiTests
{
    [TestMethod]
    public void SwaggerDocumentsAllFourSearchEndpoints()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(SearchController).Assembly);
        builder.Services.AddSwaggerGen();
        using WebApplication app = builder.Build();

        ISwaggerProvider swagger = app.Services.GetRequiredService<ISwaggerProvider>();
        Microsoft.OpenApi.OpenApiDocument document = swagger.GetSwagger("v1");

        Assert.IsTrue(document.Paths.ContainsKey("/api/search"));
        Assert.IsTrue(document.Paths.ContainsKey("/api/reverse"));
        Assert.IsTrue(document.Paths.ContainsKey("/api/nearby"));
        Assert.IsTrue(document.Paths.ContainsKey("/api/categories"));
    }
}
