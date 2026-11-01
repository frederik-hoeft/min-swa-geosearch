# GeoSearch

GeoSearch is a .NET 10 / Blazor WebAssembly application for [Software Architectures Lab 2](lab/lab-2.md). It will provide address, place, and nearby search over the instructor's OpenStreetMap-derived Hannover dataset through a PostGIS-backed API and a MapLibre map.

**Current scope:** the solution, development environment, Identity integration, and styled browser shell are implemented. The search API, GeoJSON results, and interactive map are planned; the map screen is a placeholder. Identity is included but is not required by the lab's read-only GIS functionality.

## Getting started

**Use the VS Code Dev Containers environment.** It includes .NET 10, PostgreSQL client tools, GDAL, `just`, and the Docker tooling. The devcontainer also starts PostGIS. Visual Studio 2026 is an optional debugging environment using the same database.

1. Open this repository in VS Code and select **Dev Containers: Reopen in Container**.
2. Download the required datasets following [the geodata instructions](lab/data/README.md), or skip the imports if you only want to test authentication.
3. From a terminal **inside the devcontainer**, initialize the GIS database:

   ```bash
   cd /workspace/lab
   just s02-db-setup
   just s03-import-gpkg
   just s04-import-addresses
   just s05-search-index
   just s06-search "Limmerstr 12 Hannover"
   ```

   `s01-stack-up` is not needed inside the devcontainer: PostGIS is already running as its Compose dependency. Importing the datasets is optional until the GIS API is implemented.
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
| `src/GeoSearch.Web.Server/` | ASP.NET Core API, Identity persistence, and authentication |
| `src/GeoSearch.Web.Client/` | Blazor WASM client and UI theme |
| `src/GeoSearch.Web.Shared/` | Client/server DTO contracts |
| `src/*Tests/` | Unit and middleware tests |
| `.devcontainer/` | Preferred development environment |
| `.agents/skills/` | Engineering-quality and documentation conventions |

For detailed instructions and system boundaries, see [development](docs/development.md), [architecture](docs/architecture.md), [validation](docs/validation.md), [UI conventions](docs/ui-design.md), and [code style](code-style.md). The instructor's original [lab task](lab/lab-2.md) remains the authority for feature requirements.
