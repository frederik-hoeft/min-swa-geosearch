using Wkg.EntityFrameworkCore.Discovery.SourceGeneration;

namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Source-generated discovery restricted to geospatial read projections.</summary>
[ModelLoader(AssemblyDiscoveryFailureBehavior = AssemblyDiscoveryFailureBehavior.Error,
    TargetAssemblies = ["GeoSearch.Web.Server"])]
[ModelDiscoveryFilter<SearchReadModelAttribute>]
public sealed partial class SearchReadModelLoader;
