# Development guide

The preferred environment is [the VS Code devcontainer](../.devcontainer/README.md). It supplies .NET 10, `just`, GDAL, PostgreSQL tools, and a Compose-managed PostGIS instance. Windows Visual Studio can optionally run and debug the web applications against the same database. The external GIS datasets are needed to exercise the search endpoints but **not** to compile the backend or run unit tests.

## Start the devcontainer and prepare the GIS database

Before opening the devcontainer, copy `.devcontainer/.env.example` to `.devcontainer/.env` and set `GEOSEARCH_READER_PASSWORD` to a locally managed password of at least 24 characters. Then open the repository in VS Code, choose **Dev Containers: Reopen in Container**, and open a terminal at `/workspace`. PostGIS starts as a Compose dependency and is reachable through `postgis:5432`. The `s01-stack-up` recipe refuses to run inside the devcontainer because its Compose project already provides PostGIS.

For the geospatial dataset, use the pinned [Geofabrik download recipe](../lab/data/README.md), then prepare the database:

```bash
cd /workspace/lab
just s00-download-data
just s02-db-setup
just s03-import-gpkg
just s04-import-addresses
just s05-search-index
just s10-reader-setup
just s06-search 'Limmerstr 12 Hannover'
just s07-reverse 9.7320 52.3759
```

The import defaults to the Hannover bounding box. `s03-import-gpkg` and `s04-import-addresses` replace their destination tables rather than append duplicates, making completed imports repeatable for the same inputs. **Imports are not atomic across layers**: if interrupted, rerun the affected import before refreshing the index. Avoid imports against a database that must remain continuously available; a staged snapshot/swap workflow belongs in production deployment.

For a complete **destructive reset** of the local GIS dataset, run `just reset-lab RESET-LAB1` in `lab/`. This drops and recreates only `lab1` (terminating its database connections), then replays the import, SQL, smoke-test, optional GeoServer publication, refresh, and restricted-role setup recipes in numeric order. Named volumes and downloaded OSM input files are preserved. GeoServer is published if reachable; pass `yes` to require it or `no` to skip it. Download the source files with `just s00-download-data` first. Input files and the reader password are validated before the drop; however, rebuilding all imported layers is **not transactional**, so a failure partway through requires another run. This command is for disposable local data only. See [lab reset instructions](../lab/README.md#rebuild-the-disposable-gis-database).

`just s05-search-index` rebuilds the instructor's index and search/reverse functions, then installs the API's read functions (`lab/sql/07-api-reads.sql`). The SQL files execute as a single database transaction: on failure, the previous index and function state remain intact. The rebuild may block readers while replacing the materialized view. `s10-reader-setup` provisions the restricted login and is safe to rerun. A full `s05` rebuild reapplies an existing reader's view grant.

After importing a new snapshot, use `just snapshot=YYYY-MM-DD s09-refresh-index` instead of rebuilding the index, when its schema and definitions are unchanged. This updates the snapshot identifier and concurrently refreshes the view in one transaction so queries can keep reading the previous index during the refresh. Use the date matching the imported source files, not merely the current date. Repeating a refresh recalculates `refreshed_at`, even if document contents are unchanged. `just` lists the other available recipes. Neither `osm` nor `search` is migrated by EF Core.

## Configure the server

Copy `src/GeoSearch.Web.Server/appsettings.example.json` to `src/GeoSearch.Web.Server/appsettings.json`. This file is ignored by Git. Alternatively set the following keys through environment variables (using double underscores). The development credentials in the instructor's Compose file are disposable; substitute your own if you changed them.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Search` | Connection to the instructor's `lab1` PostGIS database; intended for read-only queries |
| `Cors:ClientOrigin` | Allowed browser origin for cross-origin API requests in Development |

Use `Host=postgis;Port=5432` in connection strings when the **API runs inside the devcontainer**, or `Host=localhost;Port=5432` when it runs **on Windows**. The search database name is `lab1`. Keep local connection strings out of version control. The lab import role (`lab1`) can create and refresh the materialized index and must **not** be used by the read-only API. Provision the restricted login from inside `lab/`, after `s05-search-index`:

```bash
just s10-reader-setup
```

The recipe calls `scripts/setup-search-reader.sh` as the administrative `lab1` role. It requires `GEOSEARCH_READER_PASSWORD` (at least 24 characters) in the process environment, and fails before contacting the database if the value is missing or too short. It does not generate or store a password. For devcontainer use, copy `.devcontainer/.env.example` to the Git-ignored `.devcontainer/.env`, set the password there, and rebuild/recreate the devcontainer so Compose injects it into the `dev` service. The file is required for container creation; its path is resolved relative to `lab/compose.yaml`, the first Compose file. Use the same value for the API's `ConnectionStrings:Search`; provisioning does not configure the application automatically. Repeated setup retains the same password until you explicitly change the variable, at which point it rotates the database login password. The password reaches `psql` through its environment, not command-line arguments, and is never printed by the script. Keep `.env` out of version control and protect its filesystem permissions. When running `just` directly on the host (outside the devcontainer), export the variable explicitly; a Compose `.env` file is not automatically sourced by your shell. If an earlier setup generated `lab/.secrets/search-reader-password`, that file is no longer read; delete it after migrating the password to your local configuration. The old path remains ignored to prevent accidental credential commits.

Set `ConnectionStrings:Search` to `Host=postgis;Database=lab1;Username=geosearch_reader;Password=<locally managed secret>` when running in the devcontainer (replace the host when running outside). The role receives only the required schema access, read permissions and function execution permissions, with read-only transactions as its session default. PostgreSQL enforces the actual read-only boundary; the EF context also rejects `SaveChanges`. Test the account by running a search and then attempting an `INSERT` into `osm.snapshot`, which must be refused. The instructor's imports and refreshes continue as `lab1`; `s05-search-index` regrants view access after a full materialized-view rebuild.

## Run the applications

The server and Blazor projects have separate HTTPS launch profiles. HTTPS is recommended for browser debugging and will be required by the Geolocation API except on `http://localhost`.

**Optional Windows Visual Studio debugging (the simplest browser setup):** keep the devcontainer running for PostGIS and imports, configure server connection strings for `localhost`, and trust the Windows SDK development certificate:

```powershell
dotnet dev-certs https --trust
```

Start `GeoSearch.Web.Server` and `GeoSearch.Web.Client` as multiple startup projects in Visual Studio. The API uses `https://localhost:5100`, and the client uses `https://localhost:5200`. The browser shell currently makes no API calls; the future search UI will use `Cors:ClientOrigin=https://localhost:5200` for cross-origin requests.

**Running within the devcontainer:** .NET builds and tests work without HTTPS certificates. Browser debugging additionally requires a trusted certificate and Kestrel binding to the container network interfaces. Docker publishes the in-container API/client ports as host ports 15100/15200. Configure Kestrel to listen securely on the container's ports 5100/5200. When the search UI is implemented, configure the client's API origin and the server's Development CORS origin (`https://localhost:15200`) consistently. If Docker host ports were customized, use the actual published ports. The [devcontainer port guide](../.devcontainer/README.md#application-ports-and-host-debugging) describes the mapping and certificate boundary.

When using the configured launch profiles in a suitable HTTPS environment, the projects can also be started independently from the repository root:

```bash
dotnet run --project src/GeoSearch.Web.Server --launch-profile GeoSearch.Web.Server
dotnet run --project src/GeoSearch.Web.Client --launch-profile GeoSearch.Web.Client
```

Run these commands in separate terminals. In a single-origin deployment, `/api/*` should be routed by the reverse proxy to the server; the future web API client can use relative URLs.

## Verify the application shell

Open `https://localhost:5100/api/health`, `https://localhost:5100/swagger`, and `https://localhost:5200/` (for Windows host debugging). The public explore page remains a noninteractive placeholder and does not yet call the API. The GIS endpoints do not require login; query `https://localhost:5100/api/search?q=Limmerstr%2012%20Hannover&limit=5` or `https://localhost:5100/api/nearby?lat=52.3759&lon=9.7320&radius=500` with the prepared database. The four routes are documented in [architecture](architecture.md#gis-http-contract).

## Build and test

From `/workspace` inside the devcontainer:

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

GIS service/controller tests are kept alongside the other server tests in `src/GeoSearch.Web.Server.Tests`, with `Moq` 4.21.0 as a test-only package dependency. The offline package archive must include Moq and its transitive dependencies to restore and execute the full solution test suite.

SCSS is compiled as part of the .NET build. Editing generated CSS directly is unsupported; source styling conventions are in [the UI guide](ui-design.md).

### Dependency archive helper

`./src/restore.sh` is an optional helper that creates an ignored `src/nuget-packages.zip` archive from a clean NuGet restore. It is not needed for normal development or CI, which use `dotnet restore` directly.

## Database ownership

The database schema and search functions are managed by the instructor's `just` recipes and SQL files, not EF Core migrations. The API maps only read-only search projections. PostgreSQL connection authentication for `geosearch_reader` is required even though the HTTP API is public.

Production deployment, trusted reverse-proxy forwarding headers, and TLS termination are separate concerns from the disposable development setup. The current repository does not include a production container recipe.
