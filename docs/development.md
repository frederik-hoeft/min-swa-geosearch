# Development setup and readiness

The initial bootstrap was generated without a .NET SDK or NuGet package feed. Package restore, compilation, EF migration generation, runtime authentication, browser rendering and integration testing are **not yet verified**. Treat source packages and runtime behavior as provisional until the first offline build and audit.

## Prerequisites

.NET SDK 10, PostgreSQL 17/PostGIS 3.5, Docker Compose, GDAL/ogr2ogr, psql, and the externally supplied Geofabrik extracts. An optional local tile/style source is required for a fully offline basemap; the current map page is a placeholder.

## Configuration

Server configuration is read through normal ASP.NET Core providers. Copy `src/GeoSearch.Web.Server/appsettings.example.json` to the ignored `appsettings.json` and provide:

- `ConnectionStrings__Search`: read-only access to the instructor-provisioned `lab1` database (not migrated by EF). Use a read-only database role in deployments.
- `ConnectionStrings__Identity`: PostgreSQL connection for app-owned tables in `app_auth` schema; the role must be permitted to create/migrate that schema. The lab's `osm` and `search` schemas are not EF-owned.
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`: issuer/audience values and a secret random key of at least 32 bytes. Never check it in. For local development use `dotnet user-secrets` or environment variables.
- `Bootstrap__Email`, `Bootstrap__Password`: optional one-time development-only account provisioner (password >= 16 chars). Never include values in logs, CI or committed configuration. The bootstrapper refuses to run outside Development.

Create a **separate empty development database** by running `./scripts/setup-identity-db.sh` after `./scripts/lab.sh stack-up`. With `Bootstrap:EnsureCreated=true` and ASPNETCORE_ENVIRONMENT=Development, startup calls `EnsureCreatedAsync` against that database so login can run before EF tooling is available. **This is development-only and is not an EF migration.** It cannot be upgraded in place to a migration-managed schema. Before deployment, discard/recreate the temporary auth database, generate and review an initial migration using `dotnet ef migrations add InitialIdentity --project src/GeoSearch.Web.Server --startup-project src/GeoSearch.Web.Server --context IdentityDbContext --output-dir Data/Migrations`, disable `EnsureCreated`, and apply migrations. No generated migration is included in this bootstrap.

Run the server at `http://localhost:5100` and the client at `http://localhost:5200` with its development API base set through `wwwroot/appsettings.json`. A single-origin production deployment should use `/api/` on the frontend host, not a separately exposed database or GIS service. Local development CORS is restricted to the configured client origin.

## Auth scope

The bootstrap has a login endpoint and a JWT-protected account endpoint. The client holds its access token in memory and clears it on logout; there are no refresh cookies or remember-me persistence. This is suitable for initial wiring and tests, not the final authentication design. Rate limits, TLS, monitoring, key rotation and reset/recovery flows require later review. The lab itself does not require login and the geospatial read endpoints should not be gated without a good reason.

WKG ASP.NET Core and EF Core packages are GPLv3 and require a licensing review for any public distribution. The RECAP source generator is registered for future application-owned models; no provider-specific PostGIS stored procedure mapper is wired into the bootstrap.

## Initial review gate

- Confirm NuGet versions/restore and update only with evidence.
- Run `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`.
- Generate the Identity migration, run it against an isolated PostgreSQL database, test login and access-token expiry with a real HTTP host.
- Verify Swagger/OpenAPI, local CORS, auth failures, no committed keys, and no coordinate query-string logging.
- For GIS, use a real imported PostGIS dataset and verify the ten instructor fixtures and latency separately.
