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

## Preferred development environment

The [development container](../.devcontainer/README.md) is the supported starting point. Reopen the repository in VS Code using Dev Containers; it provides a Debian-based .NET 10 SDK, PostgreSQL command-line tools, GDAL, and the project shell setup. The Compose project also starts PostGIS. Run `./scripts/lab.sh` and `./scripts/setup-identity-db.sh` from the container terminal, using `Host=postgis` for database connections made inside the container.

For Windows host debugging, keep the devcontainer running for PostGIS and GIS tooling, but run the ASP.NET API and Blazor client using Visual Studio's HTTPS launch profiles. Host applications connect to PostgreSQL through the published `localhost:5432` port; Docker publishes any apps started inside the container on separate 15100/15200 host ports. See the [port and certificate guide](../.devcontainer/README.md#application-ports-and-host-debugging). The Docker socket grants access to the host daemon: this environment is for trusted local development, not untrusted code or production deployment.

## SCSS

Blazor global styles live in `UI/Styles/app.scss`, shared component styles are imported
from there, and page-specific isolated styles live in adjacent `*.razor.scss` files.
`sasscompiler.json` explicitly declares those outputs. See the standalone
[UI conventions and identity](ui-design.md) for component locality, themes and layout patterns.
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
- `Auth__RefreshTokenCleanup__Retention`, `Auth__RefreshTokenCleanup__Interval`: optional retention after token expiry (default 30 days) and background cleanup interval (default one day). Expired history is purged without touching unexpired sessions.

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

The host development server runs at `https://localhost:5100`, and the Blazor client at `https://localhost:5200`. The client defaults to its own hosting origin in `wwwroot/appsettings.json`; `wwwroot/appsettings.Development.json` overrides the API origin to `https://localhost:5100/` for host debugging. In same-origin deployments, omit the override and route `/api/*` through the frontend origin. Development CORS is restricted to the configured
HTTPS client origin. **Both applications must use HTTPS**: antiforgery refuses HTTP requests
because its cookie uses `SecurePolicy=Always`, and the host-scoped refresh token is a
`Secure`, `HttpOnly`, `SameSite=Strict` cookie. Mixing HTTP and HTTPS on localhost also
makes refresh requests cross-site under schemeful SameSite rules.

On Windows, trust the SDK-provided ASP.NET Core HTTPS development certificate once:

```powershell
dotnet dev-certs https --check --trust
# If the certificate is missing or untrusted:
dotnet dev-certs https --trust
```

Run the API and Blazor launch profiles in Visual Studio as usual. Trust is managed on the
**Windows host**, not within the devcontainer used for PostGIS tooling. If you created a
local ignored `src/GeoSearch.Web.Server/appsettings.json` or User Secrets with
`Cors:ClientOrigin=http://localhost:5200`, change the origin to
`https://localhost:5200`; this local configuration overrides the project default.
Use `localhost` consistently for both sites, not an HTTP/HTTPS or localhost/127.0.0.1
mixture.

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

5. Open `https://localhost:5100/swagger`, `https://localhost:5100/api/health`, and
   `https://localhost:5200/login`. Log in with the provisioned development account.
   The authenticated landing page is deliberately a map placeholder. Reloading the browser
   obtains a new access JWT through the secure refresh cookie.

To check the API independently, use the Swagger login route or issue a JSON POST to
`https://localhost:5100/api/auth/login`, then call `/api/auth/me` with the returned
`Authorization: Bearer <accessToken>` header. Without a token `/api/auth/me` must
return HTTP 401; invalid login credentials must not issue a token. The server applies
migrations automatically on its first startup; if that fails, inspect its startup
log and connection string before testing login.

**Visual Studio on the host:** start the two launch profiles as separate startup
projects. VS Code port forwarding is disabled in the devcontainer. Docker translates
container application ports 5100/5200 to host-loopback ports 15100/15200 instead,
so a second .NET instance running in the container cannot claim Visual Studio's
host ports. The host-side client uses the Development-only API URL `https://localhost:5100/`.
For container-hosted application testing, see
[devcontainer port configuration](../.devcontainer/README.md#application-ports-and-host-debugging)
for container binding, browser API URLs, and CORS requirements.

If `geosearch_auth` was previously initialized with `EnsureCreated`, stop the server
and recreate only that **disposable** development database before retrying. Leave
`lab1` intact. A development reset can be performed with `dropdb geosearch_auth`
followed by `./scripts/setup-identity-db.sh`, provided all connections are closed.

## Auth scope

The API uses two-minute bearer access JWTs, rotating opaque refresh tokens in secure
host-scoped cookies, and antiforgery validation for refresh/logout. The browser keeps the
access token in memory and restores a session using the refresh cookie. Logout revokes its
refresh-token family; changing the password revokes all active refresh sessions. Already
issued JWTs remain usable until expiry, so individual bearer requests are not immediately
revoked. The lab does not require login for GIS read operations; do not gate public search
endpoints without a concrete requirement.

Production deployments need HTTPS termination, trusted forwarded-header configuration, upstream/global traffic controls, key rotation, logging and monitoring. Development certificates and sample credentials must never be
used for production.

## Authentication session lifecycle

The server issues a short-lived access JWT in the login response and a 30-day, rotating refresh token as a `__Host-geosearch-refresh` cookie. The browser does not persist access tokens in Web Storage. A refresh attempt first fetches `/api/auth/antiforgery` and sends its request token in `X-CSRF-TOKEN`, with browser credentials included. The API allows credentialed CORS only from the configured development origin; use `localhost` consistently across both services and do not mix `127.0.0.1` with `localhost`. Production deployments should use HTTPS and a same-origin reverse proxy.

The login endpoint is rate-limited to five attempts per minute per connecting IP address, alongside Identity's per-account lockout. Behind a reverse proxy, the application must be configured to accept forwarded addresses only from trusted proxies; otherwise the proxy address shares one partition for all its clients. Client-supplied forwarding headers are not trusted by default.

The refresh-token maintenance service prunes records after their expiration plus a configurable 30-day retention window. This bounds history growth for long-lived rotating sessions while allowing replay detection during the retention period. Reuse of a token deleted beyond that window is rejected as unknown and cannot identify its family for revocation; tokens that have not expired are never pruned. Cleanup runs at startup and daily thereafter by default, retries after database failures, and operates only on the Identity database.

The frontend restores a session from the refresh cookie on initial load, refreshes near expiry, and logs out through `POST /api/auth/logout`. A failed logout request leaves the local session intact rather than claiming server revocation. Change a password at `/password`; the server revokes existing refresh-token families and returns a replacement session. Access JWTs are intentionally not individually revoked, so an old JWT may remain accepted for up to two minutes. Revocation and rotation require the Identity database; the read-only PostGIS database is not modified.

Manual smoke test: login, refresh the browser tab (session is restored), wait for access expiry and reopen the protected page, open two tabs and refresh both, log out and verify that refresh returns 401, then change the password and verify older sessions can no longer refresh. Confirm request-cookie/CORS/antiforgery behavior in browser developer tools.
