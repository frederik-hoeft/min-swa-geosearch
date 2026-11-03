using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.Search;
using GeoSearch.Web.Server.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Wkg.AspNetCore.Transactions.Configuration;

namespace GeoSearch.Web.Server.Tests.Search;

[TestClass]
public sealed class SearchDependencyTests
{
    [TestMethod]
    public async Task IdentityAndSearchTransactionsResolveSideBySideAsync()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=not_connected_identity;Username=test;Password=test"));
        services.AddSingleton<SearchReadModelLoader>();
        services.AddDbContext<SearchReadDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=not_connected_search;Username=test;Password=test"));
        services.AddTransactionManagement<IdentityDbContext>(options =>
            options.UseIsolationLevel(IsolationLevel.ReadCommitted));
        services.AddTransactionManagement<SearchReadDbContext>(options =>
            options.UseIsolationLevel(IsolationLevel.ReadCommitted));
        services.AddScoped<ISearchReadRepository, SearchReadRepository>();
        services.AddScoped<IGeoSearchService, GeoSearchService>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IGeoSearchService service = scope.ServiceProvider.GetRequiredService<IGeoSearchService>();

        Assert.IsInstanceOfType<GeoSearchService>(service);
    }
}
