using Microsoft.EntityFrameworkCore;

namespace GeoSearch.Web.Server.Data;

/// <summary>
/// Query-only connection to the instructor-owned OSM/search database. No mapped entities,
/// migrations, schema creation or SaveChanges path belongs to this context.
/// </summary>
public sealed class SearchReadDbContext(DbContextOptions<SearchReadDbContext> options) : DbContext(options);
