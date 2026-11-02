using GeoSearch.Web.Server.Security;
using Microsoft.Extensions.Configuration;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class JwtOptionsTests
{
    [TestMethod]
    public void RejectsShortSigningKeys()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Issuer"] = "GeoSearch.Tests",
                ["Audience"] = "GeoSearch.TestClient",
                ["SigningKey"] = "not-long-enough"
            })
            .Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => JwtOptions.FromConfiguration(configuration));
    }
}
