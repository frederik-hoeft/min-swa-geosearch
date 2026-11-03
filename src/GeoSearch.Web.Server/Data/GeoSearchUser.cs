using Microsoft.AspNetCore.Identity;

namespace GeoSearch.Web.Server.Data;

/// <summary>Application-owned Identity user; search data remains externally managed.</summary>
[IdentityModel]
public sealed partial class GeoSearchUser : IdentityUser;
