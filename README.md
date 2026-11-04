# GeoSearch

GeoSearch is a .NET 10 / Blazor WebAssembly application for [Software Architectures Lab 2](lab/lab-2.md). Its ASP.NET Core API searches the OpenStreetMap-derived Hannover dataset in PostGIS and exposes address search, reverse geocoding, nearby POIs, and category discovery as JSON/GeoJSON. The browser communicates with the API rather than accessing the database or an external geocoder.

The GIS API and development/deployment infrastructure are implemented. The client currently has an application shell and map placeholder; interactive map search and browser geolocation remain to be completed. The four GIS endpoints are anonymous and read-only.

## Run the application

For a local, single-origin HTTPS deployment, use [the Docker deployment guide](deploy/README.md). It builds the frontend and API images locally, runs PostGIS with persistent storage, and serves Blazor through Nginx. The recommended acceptance-test environment is a VS Code devcontainer controlling the host Docker daemon via Docker-outside-of-Docker (DooD).

If the dataset and TLS certificates are already provisioned, start the deployment from the repository root:

```sh
cd deploy
docker compose up -d --build
```

Open `https://localhost:8443/` on the Docker host. First-time setup requires configuring `deploy/.env`, loading a TLS certificate, and importing the pinned GIS dataset; follow the deployment guide before using the commands above on a fresh installation. The initial download and import are not included in the short startup path.

## Develop and test

The [development guide](docs/development.md) covers the VS Code devcontainer, GIS recipes, and local ASP.NET Core/Blazor debugging. From the repository root in the devcontainer:

```sh
dotnet restore src/GeoSearch.slnx
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

The [architecture](docs/architecture.md) explains the search pipeline, API contract, and database ownership. [Validation](docs/validation.md) describes automated checks and runtime acceptance. [Lab handoff](docs/lab-handoff.md) tracks the architecture note and evidence required for submission. For UI styling and source conventions, see [UI design](docs/ui-design.md) and [code style](code-style.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/GeoSearch.Web.Server/` | ASP.NET Core API and read-only GIS data access |
| `src/GeoSearch.Web.Client/` | Blazor WebAssembly frontend |
| `src/GeoSearch.Web.Shared/` | Shared HTTP contracts and GeoJSON models |
| `src/*Tests/` | Unit and contract tests |
| `lab/` | Instructor specification, data import, PostGIS SQL, and `just` recipes |
| `.devcontainer/` | Development toolchain and services |
| `deploy/` | Local Compose stack, Dockerfiles, Nginx, and provisioning scripts |
