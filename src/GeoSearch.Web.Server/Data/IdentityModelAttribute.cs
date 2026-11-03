using Wkg.EntityFrameworkCore.Configuration.Reflection.Attributes;

namespace GeoSearch.Web.Server.Data;

/// <summary>Marker for models owned by the writable Identity database.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class IdentityModelAttribute : DatabaseEngineModelAttribute;
