# GeoSearch

GeoSearch is a .NET 10 / Blazor WebAssembly project for the Software Architectures Lab 2: local search and nearby queries over an OSM-derived Hannover PostGIS snapshot. The planned API exposes GeoJSON for a MapLibre-based map client. The repository currently provides the application, authentication, and development foundation; the GIS endpoints and interactive map remain to be implemented.

## Structure

- `lab/`: unchanged instructor SQL, Compose, import settings and original `justfile` (for provenance); large datasets are not included.
- `scripts/lab.sh`: Bash replacement for the instructor's `just` recipes; see `lab/data/README.md` for required downloads.
- `src/GeoSearch.Web.Server/`: ASP.NET Core HTTP API and app-owned Identity database.
- `src/GeoSearch.Web.Client/`: Blazor WebAssembly UI with code-behind views, a branded login experience and a clearly marked map placeholder.
- `src/GeoSearch.Web.Shared/`: DTOs shared by the client and server.
- `src/GeoSearch.Web.Server.Tests/`: MSTest scaffolding.
- `docs/architecture.md`: intended ownership, boundaries and deferred GIS features.

## Lab database

Requires Docker Compose, `psql`, `ogr2ogr` (GDAL), and `bash`. The optional GeoServer publishing command also uses `curl` and `python3`. Copy the two external datasets referenced in `lab/data/README.md` to their expected paths. The lab Compose includes a PostGIS instance and optional GeoServer (`./scripts/lab.sh geoserver-up`); database credentials in `lab/compose.yaml` are local-only defaults and must not be deployed unchanged.

```bash
./scripts/lab.sh stack-up
./scripts/lab.sh db-setup
./scripts/lab.sh import-gpkg
./scripts/lab.sh import-addresses
./scripts/lab.sh search-index
./scripts/lab.sh search 'Limmerstr 12 Hannover'
```

## Preferred development workflow

Use the [development container](.devcontainer/README.md) for the .NET 10 toolchain, PostgreSQL/PostGIS, GDAL, and lab import scripts. Open the repository in VS Code with **Dev Containers: Reopen in Container**. The workspace is mounted at `/workspace`, and the database is available as `postgis:5432` inside the container.

Visual Studio on Windows is an optional frontend and debugger: run the API and Blazor projects on the host at `https://localhost:5100` and `https://localhost:5200` while the devcontainer and database remain running. Docker publishes PostgreSQL on host port 5432. The container's application ports use separate host mappings to avoid conflicts. See the [development guide](docs/development.md) for setup and HTTPS requirements.

## Build and application configuration

```bash
dotnet restore src/GeoSearch.slnx
dotnet build src/GeoSearch.slnx --no-restore
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

To collect all direct and transitive NuGet packages for an offline build, run `./src/restore.sh` on a machine with a .NET 10 SDK, Python 3 and NuGet.org access. The script restores the complete solution using a fresh isolated cache and writes the ignored `src/nuget-packages.zip` bundle for handoff. See [development setup](docs/development.md#offline-dependency-bundle) for limitations.

The ASP.NET server requires an application-owned PostgreSQL connection string and JWT signing key via local configuration, User Secrets, or environment variables. Copy `src/GeoSearch.Web.Server/appsettings.example.json` to a local, git-ignored `appsettings.json` and supply the private settings described in `docs/development.md`. The authentication API uses two-minute JWTs and rotating, HttpOnly-cookie refresh sessions; the development account can be provisioned at startup. Public registration is disabled. No hard-coded production credentials or signing key are included.

When developing the web apps, run the server and the separate WASM client in two terminals or as Visual Studio startup projects. The client uses the hosting origin by default and an explicit API origin in Development. For same-origin deployment, the frontend is served as static assets and `/api/*` is reverse proxied to the server; a production container recipe has not been created. Run `./scripts/setup-identity-db.sh` to provision the independent Identity database; the API applies the committed EF migration at startup (see development documentation). The refreshed offline dependency bundle includes SassCompiler. Build, formatting and MSTest are verified independently of the browser and PostgreSQL integration checks. See [validation](docs/validation.md) for exact scope and the outstanding database/runtime verification.

## References

- [Architecture](docs/architecture.md)
- [Development and remaining bootstrap work](docs/development.md)
- [Bootstrap validation and limitations](docs/validation.md)
- [Lab requirements](lab/lab-2.md)
- [Code style](code-style.md)
- [Project skills](.agents/skills/)
- [UI visual identity and component conventions](docs/ui-design.md)
