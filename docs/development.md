# Development guide

The preferred environment is [the VS Code devcontainer](../.devcontainer/README.md). It supplies .NET 10, `just`, GDAL, PostgreSQL tools, and a Compose-managed PostGIS instance. Windows Visual Studio can optionally run and debug the web applications against the same database. The external GIS datasets are needed for imports but **not** for the authentication and UI-shell smoke test.

## Start the devcontainer and prepare the databases

Open the repository in VS Code, choose **Dev Containers: Reopen in Container**, and open a terminal at `/workspace`. PostGIS starts as a Compose dependency and is reachable through `postgis:5432`. Do **not** run the instructor's `s01-stack-up` recipe inside the devcontainer; it would try to start a separate stack.

For the geospatial dataset, follow [the download instructions](../lab/data/README.md), then run:

```bash
cd /workspace/lab
just s02-db-setup
just s03-import-gpkg
just s04-import-addresses
just s05-search-index
just s06-search 'Limmerstr 12 Hannover'
just s07-reverse 9.7320 52.3759
```

The import defaults to the Hannover bounding box, and the SQL and search functions are owned by the instructor's lab scripts. `just` lists other available recipes; `just s09-refresh-index` refreshes the materialized search index after a subsequent import. The imported `osm` and `search` schemas are never migrated by EF Core.

Create the separate application-owned Identity database from the repository root:

```bash
cd /workspace
./scripts/setup-identity-db.sh
```

The script is idempotent. At API startup, EF Core applies committed migrations to the `app_auth` schema in `geosearch_auth`. Do not use `EnsureCreated` for this migrated schema. If a previous *disposable* development database was created using `EnsureCreated`, stop the API, drop **only** `geosearch_auth` while no clients are connected, and recreate it using this script. Never reset the imported `lab1` database as part of Identity troubleshooting.

## Configure the server

Copy `src/GeoSearch.Web.Server/appsettings.example.json` to `src/GeoSearch.Web.Server/appsettings.json`. This file is ignored by Git. Alternatively set the following keys through environment variables (using double underscores) or .NET User Secrets. The development credentials in the instructor's Compose file are disposable; substitute your own if you changed them.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Identity` | Writable connection to `geosearch_auth` for Identity and refresh-token persistence |
| `ConnectionStrings:Search` | Connection to the instructor's `lab1` PostGIS database; intended for read-only queries |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` | JWT validation values; the signing key must be at least 32 bytes |
| `Bootstrap:Email`, `Bootstrap:Password` | Optional Development-only initial account; password minimum is eight characters |
| `Cors:ClientOrigin` | Allowed browser origin for credentialed requests in Development |
| `Auth:RefreshToken:*` | Secure refresh cookie name and lifetime |
| `Auth:RefreshTokenCleanup:*` | Expired-token retention (30 days by default) and cleanup interval (one day by default) |

Use `Host=postgis;Port=5432` in connection strings when the **API runs inside the devcontainer**, or `Host=localhost;Port=5432` when it runs **on Windows**. The Identity database name is `geosearch_auth`; the search database name is `lab1`. The local `appsettings.json` is not shared between separately configured Windows and Linux user-secret stores.

The bootstrap account is only created when absent. Changing `Bootstrap:Password` does not reset an existing password. Public registration is disabled, and the lab's GIS read operations do not depend on Identity.

## Run the applications

The server and Blazor projects have separate HTTPS launch profiles. Both browser origins must use `localhost` and HTTPS: the antiforgery and refresh cookies have Secure and SameSite=Strict policies.

**Optional Windows Visual Studio debugging (the simplest browser setup):** keep the devcontainer running for PostGIS and imports, configure server connection strings for `localhost`, and trust the Windows SDK development certificate:

```powershell
dotnet dev-certs https --trust
```

Start `GeoSearch.Web.Server` and `GeoSearch.Web.Client` as multiple startup projects in Visual Studio. The API uses `https://localhost:5100`, and the client uses `https://localhost:5200`. The committed `wwwroot/appsettings.Development.json` sets the API origin for this workflow. An ignored local server `appsettings.json` or User Secrets value must specify `Cors:ClientOrigin=https://localhost:5200` if it overrides the default.

**Running within the devcontainer:** .NET builds and tests work without HTTPS certificates. Browser debugging additionally requires a trusted certificate and Kestrel binding to the container network interfaces. Docker publishes the in-container API/client ports as host ports 15100/15200. Override the client's Development `Api:BaseUrl` with `https://localhost:15100/` and the API's Development CORS origin with `https://localhost:15200`, and configure Kestrel to listen securely on the container's port 5100/5200. The [devcontainer port guide](../.devcontainer/README.md#application-ports-and-host-debugging) describes the mapping and certificate boundary.

When using the configured launch profiles in a suitable HTTPS environment, the projects can also be started independently from the repository root:

```bash
dotnet run --project src/GeoSearch.Web.Server --launch-profile GeoSearch.Web.Server
dotnet run --project src/GeoSearch.Web.Client --launch-profile GeoSearch.Web.Client
```

Run these commands in separate terminals. In a single-origin deployment, the client defaults to its hosting origin and `/api/*` should be routed by the reverse proxy to the server; do not ship the Development-only API URL override.

## Verify the application shell

With the Identity database available and a Development bootstrap account configured, open `https://localhost:5100/api/health`, `https://localhost:5100/swagger`, and `https://localhost:5200/login` (for Windows host debugging). Log in, reload the client, and verify that the session is restored. The authenticated map screen is currently a noninteractive placeholder.

The server issues a two-minute bearer access JWT and a rotating, HttpOnly, Secure refresh cookie. Refresh/logout operations use the antiforgery request-token header `X-CSRF-TOKEN`, and session rotation/revocation use the Identity database. An access JWT remains valid until expiry even after logout or password change. The API does not apply application-level rate limiting; Identity retains per-account lockout. See [architecture](architecture.md#authentication-and-session-lifecycle) for the security model and [validation](validation.md#runtime-verification) for the integration checks still required.

## Build and test

From `/workspace` inside the devcontainer:

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

SCSS is compiled as part of the .NET build. Editing generated CSS directly is unsupported; source styling conventions are in [the UI guide](ui-design.md).

### Offline dependency bundle

The normal devcontainer uses NuGet restore and caches packages on a persistent volume. For an isolated environment without NuGet access, `./src/restore.sh` can collect direct and transitive packages from NuGet.org into an ignored `src/nuget-packages.zip`; this preparation requires .NET SDK 10, Bash, Python 3, and network access. The SDK itself is distributed separately.

Extract the SDK and package archive into isolated locations, use a temporary NuGet configuration clearing all package sources, and point `DOTNET_ROOT`, `PATH`, and `NUGET_PACKAGES` at the prepared copies. Restore once using that configuration, then build and test with `--no-restore`:

```bash
dotnet restore src/GeoSearch.slnx --configfile "$OFFLINE_NUGET_CONFIG" --force --no-cache
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

The archive must include all transitive build tools, including `AspNetCore.SassCompiler`; unpacking packages directly into NuGet's global-cache layout requires restoring the Sass compiler executable permission on Linux. See [the offline engineering instructions](https://github.com/frederik-hoeft/custom-skills/blob/main/dev/dotnet-offline.skill) for package-cache preparation. These archives are not committed to Git.

## Database model changes

EF Core migrations are restricted to the application-owned Identity schema. For future changes, use a `dotnet-ef` tool version matching the installed EF Core packages and scaffold migrations for `IdentityDbContext` in `src/GeoSearch.Web.Server/Data/Migrations`. Review and commit both the migration and model snapshot; do not generate migrations for the instructor-owned `osm` or `search` schemas.

Production deployment, key management, trusted reverse-proxy forwarding headers, and TLS termination are separate concerns from the disposable development setup. The current repository does not include a production container recipe.
