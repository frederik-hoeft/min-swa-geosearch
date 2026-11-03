using Wkg.EntityFrameworkCore.Configuration.Reflection.Attributes;

namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Marker for models owned by the read-only geospatial database.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class SearchReadModelAttribute : DatabaseEngineModelAttribute;
