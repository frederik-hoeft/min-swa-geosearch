using Microsoft.EntityFrameworkCore;
using Wkg.EntityFrameworkCore.Discovery.SourceGeneration;

namespace GeoSearch.Web.Server.Data;

/// <summary>
/// Compile-time discovery for future application-owned EF configurations.
/// Imported OSM entities are deliberately excluded from discovery and migrations.
/// </summary>
[ModelLoader(AssemblyDiscoveryFailureBehavior = AssemblyDiscoveryFailureBehavior.Error,
    TargetAssemblies = ["GeoSearch.Web.Server"])]
internal sealed partial class GeoSearchModelLoader;
