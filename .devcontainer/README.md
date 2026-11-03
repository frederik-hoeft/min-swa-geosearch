# GeoSearch development container

The devcontainer is the **preferred development environment**. It provides a .NET 10 SDK, `just`, GDAL/`ogr2ogr`, PostgreSQL clients, Git, and Docker CLI access. Docker Compose starts a PostGIS service alongside the workspace container and mounts the repository at `/workspace`. The optional GeoServer service publishes GIS data independently of the application API.

Visual Studio on Windows can be used for host-side debugging without replacing the container-based GIS toolchain.

## Start the environment

1. Install Docker with Compose, VS Code, and the Dev Containers extension.
2. Copy `.devcontainer/.env.example` to `.devcontainer/.env` and set `GEOSEARCH_READER_PASSWORD` to a strong, locally managed value of at least 24 characters. The `.env` file is ignored by Git; restrict its permissions because it contains a secret.
3. Open the repository in VS Code and choose **Dev Containers: Reopen in Container**.
4. In the container terminal, verify the tools and database:

   ```bash
   dotnet --version
   just --version
   ogr2ogr --version
   psql -Atc 'SELECT 1'
   ```

   PostGIS is reachable at `postgis:5432` from the devcontainer, with the lab defaults `lab1` for both the database and user. The Compose service publishes PostgreSQL at `localhost:5432` on the Windows host.

The initial container build needs network access for images, extensions, and shell setup. NuGet packages are cached in a named volume. The container shares the host Docker daemon via its socket and is suitable only for trusted local development. Git's safe-directory exception is limited to `/workspace`.

## Run the GIS recipes

The instructor's recipes live in `lab/justfile`. The container includes `just`, so no translation or additional script is necessary:

```bash
cd /workspace/lab
just                       # List available recipes
just s00-download-data  # One-time download; skips verified existing files
just s02-db-setup
just s03-import-gpkg
just s04-import-addresses
just s05-search-index
just s10-reader-setup
just s06-search 'Limmerstr 12 Hannover'
just s07-reverse 9.7320 52.3759
```

To start over with an empty GIS database and replay the entire pipeline, use `just reset-lab RESET-LAB1` from `/workspace/lab`. This intentionally destroys **only** `lab1`; it preserves the separate Identity database. The GeoServer step runs when GeoServer is reachable. See [the reset procedure](../lab/README.md#rebuild-the-disposable-gis-database) for prerequisites and optional controls.

The `s00-download-data` recipe downloads and verifies the [external Geofabrik data](../lab/data/README.md) before import; it does not require PostgreSQL or run in CI. `s01-stack-up` is guarded and refuses to run in the devcontainer, where its Compose project has already started PostGIS. It remains available for standalone lab setups.

The dev service explicitly loads `.devcontainer/.env` through its optional Compose `env_file` setting; this makes `GEOSEARCH_READER_PASSWORD` available to `just` **inside** the devcontainer after it is recreated. A Compose `.env` file by itself does not export variables into a host shell, and the `env_file` injection does not automatically configure the API's `ConnectionStrings:Search`. See [development configuration](../docs/development.md#configure-the-server). Do not assume this dev-service `env_file` supplies `POSTGIS_PASSWORD` to the separate PostGIS service; that service uses Compose interpolation configured separately.

The optional GeoServer service uses the same internal Docker network. If it is started through the devcontainer's Compose project, `GEOSERVER_URL=http://geoserver:8080/geoserver` directs the `s08-geoserver-publish` recipe to it. GeoServer is not needed for application search or the initial imports.

The separate Identity database is created from the repository root with `./scripts/setup-identity-db.sh`; the API applies EF migrations to it at startup. See [development configuration](../docs/development.md#configure-the-server).

## Application ports and host debugging

Docker publishes application ports on host-loopback addresses distinct from the Visual Studio launch-profile ports.

| Application | In container | Container published on host | Visual Studio on host |
| --- | --- | --- | --- |
| ASP.NET Core API | `5100` | `15100` | `5100` |
| Blazor WASM client | `5200` | `15200` | `5200` |
| PostgreSQL | `5432` on `postgis` | `5432` | `5432` |

The application mappings use `GEOSEARCH_DEV_API_PORT` and `GEOSEARCH_DEV_CLIENT_PORT` to override the published host ports. VS Code automatic port forwarding is disabled to prevent it from taking Visual Studio's ports. A Docker port mapping carries TCP traffic but does not terminate TLS.

**Recommended Windows debugging workflow:** leave the devcontainer and PostGIS running, open the solution in Visual Studio on Windows, and start the API and client HTTPS launch profiles at `https://localhost:5100` and `https://localhost:5200`. Configure their server connection strings with `Host=localhost`; trust the Windows development certificate using `dotnet dev-certs https --trust`. No container certificate is required for this workflow. Copy the client Development configuration example as described in the [development guide](../docs/development.md#run-the-applications); it points to the host API. The server allows the `https://localhost:5200` CORS origin.

**Running the web apps inside the devcontainer:** requires configuring an HTTPS certificate that the browser trusts, and binding Kestrel to container-accessible interfaces rather than loopback only. The browser URLs become `https://localhost:15100` and `https://localhost:15200` by default. Set the API origin to `https://localhost:15100/` in the ignored `wwwroot/appsettings.Development.json` (created from the example), and the server's `Cors:ClientOrigin` to `https://localhost:15200`. This does not require editing tracked files. Both sites must use HTTPS and `localhost` consistently for the Secure, SameSite=Strict authentication cookies. Details are in [the development guide](../docs/development.md#run-the-applications).

The container's applications do not start automatically. For the host-debugging workflow, use the devcontainer for GIS recipes and tooling, and run the .NET processes on Windows.
