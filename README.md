# GeoSearch

GeoSearch is a .NET 10 / Blazor WebAssembly application for [Software Architectures Lab 2](lab/lab-2.md). It exposes address, place, reverse-geocoding, and nearby search over the instructor's OpenStreetMap-derived Hannover dataset through a PostGIS-backed API. A MapLibre map is planned for the Blazor client.

**Current scope:** the solution, development environment, Identity integration, browser shell, and four read-only GIS API endpoints are implemented. Interactive search/map/geolocation UI remains a placeholder. Identity is included but is not required by the lab's GIS API.

## Getting started

**Use the VS Code Dev Containers environment.** It includes .NET 10, PostgreSQL client tools, GDAL, `just`, and the Docker tooling. The devcontainer also starts PostGIS. Visual Studio 2026 is an optional debugging environment using the same database.

1. Copy `.devcontainer/.env.example` to `.devcontainer/.env` and set a strong `GEOSEARCH_READER_PASSWORD` (at least 24 characters). The file is ignored by Git. Then open the repository in VS Code and select **Dev Containers: Reopen in Container**.
2. Inside the devcontainer, run `cd /workspace/lab && just s00-download-data` to fetch the two pinned Geofabrik datasets (about 1.3 GB). See [the geodata instructions](lab/data/README.md). Skip this step if you only need to build or test the application.
3. From a terminal **inside the devcontainer**, initialize the GIS database:

   ```bash
   cd /workspace/lab
   just s02-db-setup
   just s03-import-gpkg
   just s04-import-addresses
   just s05-search-index
   just s10-reader-setup
   just s06-search "Limmerstr 12 Hannover"
   ```

   To discard and rebuild the local GIS database later, run `just reset-lab RESET-LAB1` from `lab/`. This deletes **only** `lab1`, not the Identity database or downloaded data. See [lab reset instructions](lab/README.md#rebuild-the-disposable-gis-database).

   `s01-stack-up` is disabled inside the devcontainer: PostGIS is already running as its Compose dependency. The `s10-reader-setup` recipe provisions an independent, restricted login using the required `GEOSEARCH_READER_PASSWORD` from the devcontainer environment; it never generates or stores credentials itself. The GIS API needs the imported dataset and search index to return results; authentication and API compilation do not.
4. Configure the server and start the web projects following the [development guide](docs/development.md). To initialize the separate Identity database, run `./scripts/setup-identity-db.sh` from `/workspace`.

The devcontainer forwards PostgreSQL to Windows at `localhost:5432`. Its web app ports are kept separate from Visual Studio's ports; see the [container and host debugging guide](.devcontainer/README.md).

## Build and test

From the repository root inside the devcontainer:

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

## Repository guide

| Path | Purpose |
| --- | --- |
| `lab/` | Instructor lab specification, SQL, Compose, and `just` recipes |
| `src/GeoSearch.Web.Server/` | ASP.NET Core API, GIS query adapter, Identity persistence, and authentication |
| `src/GeoSearch.Web.Client/` | Blazor WASM client and UI theme |
| `src/GeoSearch.Web.Shared/` | Client/server API contracts, including point-only GeoJSON DTOs |
| `src/*Tests/` | Unit, contract, and middleware tests, including GIS tests in the server test project |
| `.devcontainer/` | Preferred development environment |
| `.agents/skills/` | Engineering-quality and documentation conventions |

For detailed instructions and system boundaries, see [development](docs/development.md), [architecture](docs/architecture.md), [validation](docs/validation.md), [UI conventions](docs/ui-design.md), and [code style](code-style.md). The instructor's original [lab task](lab/lab-2.md) remains the authority for feature requirements.
