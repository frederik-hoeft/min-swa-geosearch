# Development guide

The [VS Code devcontainer](../.devcontainer/README.md) provides the .NET 10 SDK, `just`, GDAL, PostgreSQL clients, and a development PostGIS instance. It is the preferred environment for building, testing, and preparing GIS data. The API and Blazor client can also be debugged in Visual Studio on Windows against that database. For integrated HTTPS and reverse-proxy acceptance testing, use the separate [Docker deployment](../deploy/README.md).

## Prepare the development database

Before opening the devcontainer, copy `.devcontainer/.env.example` to `.devcontainer/.env` and set `GEOSEARCH_READER_PASSWORD` to a locally managed password of at least 24 characters. Reopen the repository in VS Code with **Dev Containers: Reopen in Container**. PostGIS starts as a Compose dependency and is accessible at `postgis:5432` from `/workspace`. Its host-published PostgreSQL port is `localhost:5432`.

Download the [pinned Geofabrik datasets](../lab/data/README.md), import the Hannover extract, and provision the API's read-only database role:

```sh
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

The devcontainer's Compose project already runs PostGIS, so `s01-stack-up` is not used there. Imports replace their destination tables but are **not atomic across layers**. After an interrupted import, rerun the affected recipe before refreshing the search index. `s05-search-index` installs the search/reverse functions and application read functions within a database transaction; replacing the materialized view can temporarily block readers.

For a new imported snapshot with unchanged schema and functions, `just snapshot=YYYY-MM-DD s09-refresh-index` updates the snapshot metadata and refreshes the materialized view concurrently. Use the source dataset's date, not the date you run the command. For a complete destructive reset of the disposable `lab1` database, use [the guarded reset procedure](../lab/README.md#rebuild-the-disposable-gis-database). Neither operation is an online deployment migration.

The `s10-reader-setup` recipe creates or updates `geosearch_reader` using `GEOSEARCH_READER_PASSWORD` from the process environment. The devcontainer injects the value through its Compose `env_file`; changing `.devcontainer/.env` requires recreating the devcontainer. A Compose `.env` file does not automatically set variables in a host shell. The import account `lab1` has administrative privileges and must not be used by the API for reads. PostgreSQL permissions, rather than the EF Core read-only guard alone, enforce that boundary.

## Configure the API and client

Copy `src/GeoSearch.Web.Server/appsettings.example.json` to the Git-ignored `src/GeoSearch.Web.Server/appsettings.json`, or supply the settings as environment variables using `__` in place of `:`. The API requires the `ConnectionStrings:Search` value for its `geosearch_reader` connection to database `lab1`. Development also supports `Cors:ClientOrigin` when the Blazor and API projects are hosted on different origins.

Use `Host=postgis;Port=5432` when the server runs **inside the devcontainer**, or `Host=localhost;Port=5432` for **Windows-host debugging**. A local example is:

```text
Host=postgis;Port=5432;Database=lab1;Username=geosearch_reader;Password=<your reader password>
```

Use the same password configured for `s10-reader-setup`. Keep credentials in ignored local configuration, not tracked files. The API does not automatically read `.devcontainer/.env`; that environment file supplies the GIS recipe process, and the server connection string must be configured separately.

A separately hosted Blazor client must know the browser-accessible API origin. Copy its tracked development example into the ignored runtime configuration:

```sh
cp src/GeoSearch.Web.Client/appsettings.Development.example.json \
   src/GeoSearch.Web.Client/wwwroot/appsettings.Development.json
```

The example uses the Windows API launch-profile origin `https://localhost:5100/`. The client uses `Api:BaseUrl` when configured and otherwise defaults to the browser's own origin. The [single-origin Docker deployment](../deploy/README.md) uses that fallback and does not need an API base-URL override. Browser configuration is public and must never contain database secrets.

## Run and debug

The API and Blazor client have separate HTTPS launch profiles. For Windows Visual Studio debugging, leave the devcontainer running for PostgreSQL and GIS tools, trust the SDK development certificate with `dotnet dev-certs https --trust`, and start both web projects. The API listens at `https://localhost:5100` and the client at `https://localhost:5200`; the API's Development CORS policy allows the client origin.

You can also start the projects in separate terminals from the repository root:

```sh
dotnet run --project src/GeoSearch.Web.Server --launch-profile GeoSearch.Web.Server
dotnet run --project src/GeoSearch.Web.Client --launch-profile GeoSearch.Web.Client
```

Running the web projects **inside the devcontainer** for browser debugging requires a trusted HTTPS certificate and Kestrel listening on container-accessible interfaces. The development Compose stack maps container ports 5100/5200 to host ports 15100/15200 by default. Set the client's development API base URL to `https://localhost:15100/` and the server's `Cors:ClientOrigin` to `https://localhost:15200`, adjusting for custom published ports. See the [devcontainer port guide](../.devcontainer/README.md#application-ports-and-host-debugging). Docker port publishing does not terminate TLS.

Once the GIS database is populated, test `/api/health`, `/api/search?q=Limmerstr%2012%20Hannover&limit=5`, and `/api/nearby?lat=52.3759&lon=9.7320&radius=500` on the API origin. Swagger UI is available at `/swagger` when the API runs in Development. The [API contract](architecture.md#gis-http-contract) explains endpoint parameters and response formats.

## Build and test

From the repository root inside the devcontainer:

```sh
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

GIS controller, service, read-model, and OpenAPI tests reside in `src/GeoSearch.Web.Server.Tests`; the Sass compiler generates client CSS as part of the .NET build. Follow the [UI style conventions](ui-design.md) rather than editing generated CSS. The optional `./src/restore.sh` helper packages NuGet dependencies into an ignored archive for offline use. Ordinary development and CI use `dotnet restore` directly.

The GIS `osm` and `search` schemas are owned by the lab SQL and import recipes, not by EF Core migrations. Runtime database and browser acceptance checks are covered in [validation](validation.md).
