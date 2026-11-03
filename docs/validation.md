# Validation and integration boundaries

GeoSearch has a read-only PostGIS data context, the public GeoJSON GIS endpoints, and a browser shell; interactive map and geolocation features remain pending. Validation therefore distinguishes compilation and in-memory contract checks from spatial tests requiring a populated PostGIS instance.

## Automated validation

From the repository root in the [preferred devcontainer](development.md#start-the-devcontainer-and-prepare-the-databases):

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

GitHub Actions runs three separate jobs: **lab-scripts** (parses the real Justfile, checks shell syntax, and executes Python automation tests), **build** (.NET restore, format verification, warnings-as-errors Release build), and **test** (a fresh restore and Release test run, after a successful build job). The build also compiles the client SCSS through SassCompiler.

The lab automation tests use a local HTTP fixture server to verify initial dataset download, no-network reruns, HTTP-range resume, and corruption recovery. They also mock the reset recipe's preflight rejection, ordering, and fail-fast behavior. These checks fetch no real Geofabrik data and cannot prove PostgreSQL/GDAL runtime semantics or production-scale download performance.

The automated tests exercise GeoJSON field/coordinate serialization, the keyless read-only GIS model, the GIS service and controller contracts, dependency registration, and generation of OpenAPI paths. They do not require PostgreSQL or a browser, and do not prove GIS SQL, database concurrency, or browser behavior.

`src/GeoSearch.Web.Server.Tests/Search` includes Moq-based controller validation and application-service mapping tests. They share the existing server test project and run with the normal solution once the NuGet source includes `Moq` 4.21.0 and its transitive dependencies. EF model tests independently verify that WKG's discovery filters isolate the GIS projections and that EF translates the three table-valued query functions without a live database.

## Runtime verification

Build and reopen the [devcontainer](../.devcontainer/README.md), execute the `just` import recipes against PostGIS, and confirm that Docker host-port publication does not occupy Visual Studio's application ports. These integration checks require the running development stack; automated unit tests are not substitutes for them.

## GIS runtime acceptance checks

Use `just s10-reader-setup` to provision the restricted `geosearch_reader` login after `s05-search-index` and the imports. Confirm that the role can execute the three API read functions and read categories and snapshots, but cannot write to `osm.snapshot`; the EF read-only guard alone is insufficient. `s05-search-index` installs the instructor's materialized view and search/reverse functions, then the API's additive read functions.

Verify `/api/search`, `/api/reverse`, `/api/nearby`, and `/api/categories` against the populated database. Cover required/malformed query parameters, boundary coordinate values and radius/limit values, empty results with snapshot metadata, longitude/latitude axis order, category labels, nearest-result behavior, and distance-in-metres ordering. Confirm that a refresh changes the exposed snapshot only as the materialized view changes and that coordinates are not present in API logs. Verify all four endpoints work without a user session or HTTP authentication credentials.

Collect the instructor's ten fixture queries with their actual top result and measured latency, including the three known ranking errors described in [the lab specification](../lab/lab-2.md#test-your-search-with-ten-fixture-queries). The target is a search response under 300 ms on the Hannover extract; offline unit tests cannot establish this. For nearby-search performance, capture `EXPLAIN (ANALYZE, BUFFERS)` on representative radii and categories before considering the [optional geography expression index](architecture.md#possible-nearby-search-spatial-index-not-implemented); do not modify the instructor-managed schema without permission. Keep the future frontend acceptance checks separate: map markers, attribution, permission denial, stale-request cancellation, location accuracy circle, map-centre fallback, and a screenshot of nearby search using the device position.
