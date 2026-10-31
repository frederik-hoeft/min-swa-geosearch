# GeoSearch

GeoSearch is a .NET 10 / Blazor WebAssembly project for the Software Architectures Lab 2: local search and nearby queries over an OSM-derived Hannover PostGIS snapshot. The backend exposes GeoJSON; the browser will display results with MapLibre. This repository is currently an **application bootstrap**, not a completed lab submission.

## Structure

- `lab/`: unchanged instructor SQL, Compose, import settings and original `justfile` (for provenance); large datasets are not included.
- `scripts/lab.sh`: Bash replacement for the instructor's `just` recipes; see `lab/data/README.md` for required downloads.
- `src/GeoSearch.Web.Server/`: ASP.NET Core HTTP API and app-owned Identity database.
- `src/GeoSearch.Web.Client/`: Blazor WebAssembly UI, login and empty search page.
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

## Build and application configuration

```bash
dotnet restore src/GeoSearch.slnx
dotnet build src/GeoSearch.slnx --no-restore
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

The ASP.NET server requires an application-owned PostgreSQL connection string and JWT signing key via User Secrets or environment variables. Copy `src/GeoSearch.Web.Server/appsettings.example.json` to a local, git-ignored `appsettings.json` and supply the private settings described in `docs/development.md`. JWT authentication is only provisioned for development using a locally configured bootstrap account. No hard-coded production credentials or signing key are included.

Run the server and the separate WASM client in two terminals for development. For same-origin deployment, the frontend is served as static assets and `/api/*` is reverse proxied to the server; a production container recipe has not been created. For development only, run `./scripts/setup-identity-db.sh` and configure `Bootstrap:EnsureCreated=true` on the separate auth database; production requires an EF migration after the SDK audit (see development documentation). The first .NET restore/build/test is intentionally deferred until an SDK and NuGet packages are available.

## References

- [Architecture](docs/architecture.md)
- [Development and remaining bootstrap work](docs/development.md)
- [Lab requirements](lab/lab-2.md)
- [Code style](code-style.md)
