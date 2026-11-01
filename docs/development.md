# Development setup and readiness

The solution supports a deterministic offline restore with a supplied .NET SDK and NuGet package cache. The bootstrap compilation and MSTest baseline are summarized in [validation.md](validation.md); they do not substitute for browser, database, and migration integration tests.

## Prerequisites

.NET SDK 10, PostgreSQL 17/PostGIS 3.5, Docker Compose, GDAL/ogr2ogr, psql, and the externally supplied Geofabrik extracts. An optional local tile/style source is required for a fully offline basemap; the current map page is a placeholder.

## Offline dependency bundle

Run `./src/restore.sh` on a **network-connected** machine with a .NET 10 SDK, Bash and Python 3. It restores `GeoSearch.slnx` from NuGet.org into a freshly cleared, private `src/.nuget-cache/`, collects the `.nupkg` files for direct and transitive dependencies, and produces `src/nuget-packages.zip` (plus the extracted `src/nuget-packages/` directory). The script can be launched from any current working directory. These outputs are ignored by Git.

Give the ZIP and the platform-matched .NET SDK archive to the offline build environment.
Extract the SDK outside the repository, point `DOTNET_ROOT` and `PATH` to that extraction,
and extract NuGet packages into an isolated cache. Use a temporary NuGet configuration
with `<packageSources><clear /></packageSources>`, point `NUGET_PACKAGES` to the isolated cache,
and run restore, build, and tests in order (without implicit restore after the first step):

```bash
dotnet restore src/GeoSearch.slnx --configfile "$OFFLINE_NUGET_CONFIG" --force --no-cache
dotnet build src/GeoSearch.slnx --no-restore /p:TreatWarningsAsErrors=true
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

The NuGet archive must include **every transitive package**, including tools such as
`AspNetCore.SassCompiler` that participate in the build. A directory of `.nupkg` files is
not itself the normal NuGet global-package cache: packages must be extracted and normalized
before using a source-less configuration. Never let the build silently use machine-global
packages or network feeds. The ZIP does not include the SDK or optional WASM workloads.
See [the bootstrap validation record](validation.md) for the verified offline build.

The script intentionally uses NuGet.org as its only feed. If package references require another feed in the future, review its trust and package provenance before extending the script.

## Development container

The included [development container](../.devcontainer/README.md) provides a Debian-based .NET 10 SDK
with PostgreSQL client tools and GDAL, plus the standard Zsh configuration. The same Compose
project starts the provided PostGIS service. From inside this container, use `Host=postgis`
in `ConnectionStrings__Identity` and `ConnectionStrings__Search`; host-side programs can use
`Host=localhost` with the host-loopback published port. The optional GeoServer service is
reachable on `http://geoserver:8080/geoserver` from inside the container.

The Docker socket exposes the host Docker daemon. The devcontainer is intended only for trusted
local development, not application deployment or running unreviewed code.

## SCSS

Blazor global styles live in `UI/Styles/app.scss` and component/page-specific isolated styles
in adjacent `*.razor.scss` files. `sasscompiler.json` explicitly declares both outputs.
`AspNetCore.SassCompiler` performs compilation during .NET builds, producing generated CSS
that is ignored by Git; generated CSS must not be hand-edited. The latest supplied offline NuGet bundle includes `AspNetCore.SassCompiler` version 1.103.0.
A normal offline build compiles the SCSS. If manually unpacking `.nupkg` files into the NuGet
global cache, restore the bundled Dart compiler's executable bit on Linux as described in the
[offline development skill](https://github.com/frederik-hoeft/custom-skills/blob/main/dev/dotnet-offline.skill).

## Configuration

Server configuration is read through normal ASP.NET Core providers. Copy `src/GeoSearch.Web.Server/appsettings.example.json` to the ignored `appsettings.json` and provide:

- `ConnectionStrings__Search`: read-only access to the instructor-provisioned `lab1` database (not migrated by EF). Use a read-only database role in deployments.
- `ConnectionStrings__Identity`: PostgreSQL connection for app-owned tables in `app_auth` schema; the role must be permitted to create/migrate that schema. The lab's `osm` and `search` schemas are not EF-owned.
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`: issuer/audience values and a secret random key of at least 32 bytes. Never check it in. For local development use `dotnet user-secrets` or environment variables.
- `Bootstrap__Email`, `Bootstrap__Password`: optional one-time development-only account provisioner (password at least 8 characters). Never include values in logs, CI or committed configuration. The bootstrapper refuses to run outside Development.

Create the independent Identity database by running `./scripts/setup-identity-db.sh` after `./scripts/lab.sh stack-up`. On startup the API applies the committed initial migration and any later migrations to this database. The migration creates the Identity tables under `app_auth` and leaves the instructor-owned `lab1` database unchanged. Do not use `EnsureCreated` on a database intended for migrations.

**If you previously started with `Bootstrap:EnsureCreated=true`:** those Identity tables were not registered in EF's migration history. For a disposable local database, stop the server and recreate **only `geosearch_auth`** (never delete `lab1`). Then start the server again to apply the committed migration. Back up any real Identity data before migrating an existing nonempty database; dropping is strictly a development reset.

For future model changes, install a `dotnet-ef` tool version matching your EF Core packages and scaffold a new migration using `dotnet ef migrations add <Name> --project src/GeoSearch.Web.Server --startup-project src/GeoSearch.Web.Server --context IdentityDbContext --output-dir Data/Migrations`. Review the generated SQL/operations, then commit both migration and snapshot.

After the database is prepared and .NET packages restored, configure the server's ignored `appsettings.json` or use User Secrets. For example:

```bash
./scripts/lab.sh stack-up
./scripts/setup-identity-db.sh
# Set the required credentials and a random signing key via local config or User Secrets.
# Example: openssl rand -base64 48 (never commit the result).
dotnet run --project src/GeoSearch.Web.Server --launch-profile GeoSearch.Web.Server
# In another terminal:
dotnet run --project src/GeoSearch.Web.Client --launch-profile GeoSearch.Web.Client
```

The development server runs at `http://localhost:5100`, and the client at `http://localhost:5200` with the API base set through `wwwroot/appsettings.json`. A single-origin production deployment should use `/api/` on the frontend host, not a separately exposed database or GIS service. Local development CORS is restricted to the configured client origin.

## First-run smoke test (login and API only)

The GIS API and map renderer are not yet implemented. You can verify the full login path,
WKG-backed Identity persistence, JWT authorization, CORS, and the Blazor shell without
importing the OSM extracts or starting GeoServer.

1. **Start PostGIS**: on the host, `./scripts/lab.sh stack-up`. In the devcontainer,
   PostGIS already starts as a Compose dependency. The database must be reachable over
   the expected port; the lab defaults to port 5432 on the host.
2. **Create the independent auth database**: `./scripts/setup-identity-db.sh`. This is
   idempotent and creates `geosearch_auth` without modifying `lab1`.
3. **Configure server User Secrets** (from the repo root), using `Host=localhost` when
   running the API on the host and `Host=postgis` from inside the devcontainer:

   ```bash
   SERVER=src/GeoSearch.Web.Server
   DB_HOST=localhost  # inside the devcontainer: DB_HOST=postgis
   dotnet user-secrets set --project "$SERVER" ConnectionStrings:Identity \
       "Host=$DB_HOST;Port=5432;Database=geosearch_auth;Username=lab1;Password=lab1-local-only"
   dotnet user-secrets set --project "$SERVER" ConnectionStrings:Search \
       "Host=$DB_HOST;Port=5432;Database=lab1;Username=lab1;Password=lab1-local-only"
   dotnet user-secrets set --project "$SERVER" Jwt:Issuer GeoSearch.Development
   dotnet user-secrets set --project "$SERVER" Jwt:Audience GeoSearch.Client
   dotnet user-secrets set --project "$SERVER" Jwt:SigningKey "$(openssl rand -base64 48)"
   dotnet user-secrets set --project "$SERVER" Bootstrap:Email dev@example.test
   read -rsp 'Temporary development password: ' DEV_PASSWORD; echo
   dotnet user-secrets set --project "$SERVER" Bootstrap:Password "$DEV_PASSWORD"
   unset DEV_PASSWORD
   ```

   Set a unique development password of at least 8 characters. The local Identity
   policy does not require particular character classes. The sample database credentials are **strictly local defaults**; replace them
   if you changed `POSTGIS_PASSWORD` in Compose. User Secrets are not committed to Git.
4. **Run the server and client in separate terminals** (same environment and repository):

   ```bash
   dotnet run --project src/GeoSearch.Web.Server --launch-profile GeoSearch.Web.Server
   dotnet run --project src/GeoSearch.Web.Client --launch-profile GeoSearch.Web.Client
   ```

5. Open `http://localhost:5100/swagger`, `http://localhost:5100/api/health`, and
   `http://localhost:5200/login`. Log in with the provisioned development account.
   The authenticated landing page is deliberately a map placeholder. Browser refresh
   clears the in-memory access token.

To check the API independently, use the Swagger login route or issue a JSON POST to
`http://localhost:5100/api/auth/login`, then call `/api/auth/me` with the returned
`Authorization: Bearer <accessToken>` header. Without a token `/api/auth/me` must
return HTTP 401; invalid login credentials must not issue a token. The server applies
migrations automatically on its first startup; if that fails, inspect its startup
log and connection string before testing login.

**Visual Studio on the host:** start the two launch profiles as separate startup
projects. VS Code port forwarding is disabled in the devcontainer. Docker translates
container application ports 5100/5200 to host-loopback ports 15100/15200 instead,
so a second .NET instance running in the container cannot claim Visual Studio's
host ports. The host-side client keeps its default API URL `http://localhost:5100/`.
For container-hosted application testing, see
[devcontainer port configuration](../.devcontainer/README.md#application-ports-and-host-debugging)
for container binding, browser API URLs, and CORS requirements.

If `geosearch_auth` was previously initialized with `EnsureCreated`, stop the server
and recreate only that **disposable** development database before retrying. Leave
`lab1` intact. A development reset can be performed with `dropdb geosearch_auth`
followed by `./scripts/setup-identity-db.sh`, provided all connections are closed.

## Auth scope

The bootstrap has a login endpoint and a JWT-protected account endpoint. The client holds its access token in memory and clears it on logout; there are no refresh cookies or remember-me persistence. This is suitable for initial wiring and tests, not the final authentication design. Rate limits, TLS, monitoring, key rotation and reset/recovery flows require later review. The lab itself does not require login and the geospatial read endpoints should not be gated without a good reason.

WKG ASP.NET Core and EF Core packages are GPLv3 and require a licensing review for any public distribution. The RECAP source generator is registered for future application-owned models; no provider-specific PostGIS stored procedure mapper is wired into the bootstrap.

## Initial review gate

- Confirm NuGet versions/restore and update only with evidence.
- Run `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`.
- Generate the Identity migration, run it against an isolated PostgreSQL database, test login and access-token expiry with a real HTTP host.
- Verify Swagger/OpenAPI, local CORS, auth failures, no committed keys, and no coordinate query-string logging.
- For GIS, use a real imported PostGIS dataset and verify the ten instructor fixtures and latency separately.
