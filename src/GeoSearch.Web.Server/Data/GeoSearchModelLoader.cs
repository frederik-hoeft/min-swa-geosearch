using Wkg.EntityFrameworkCore.Discovery.SourceGeneration;

namespace GeoSearch.Web.Server.Data;

/// <summary>Source-generated discovery restricted to application-owned Identity models.</summary>
[ModelLoader(AssemblyDiscoveryFailureBehavior = AssemblyDiscoveryFailureBehavior.Error,
    TargetAssemblies = ["GeoSearch.Web.Server"])]
[ModelDiscoveryFilter<IdentityModelAttribute>]
internal sealed partial class GeoSearchModelLoader;
