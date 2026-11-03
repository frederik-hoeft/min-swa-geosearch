# Validation and integration boundaries

GeoSearch has authentication, separate database contexts, the GeoJSON GIS endpoints, and a browser shell; interactive map and geolocation features remain pending. Validation therefore distinguishes compilation and in-memory contract checks from spatial tests requiring a populated PostGIS instance.

## Automated validation

From the repository root in the [preferred devcontainer](development.md#start-the-devcontainer-and-prepare-the-databases):

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

GitHub Actions restores dependencies, checks formatting, builds in Release with warnings treated as errors, and runs tests. The build also compiles the client SCSS through SassCompiler.

The automated tests exercise Identity/JWT behavior and model metadata, browser session contracts, antiforgery behavior, refresh-token boundaries, GeoJSON field/coordinate serialization, the keyless read-only GIS model, simultaneous registration of the two WKG transaction contexts, and generation of OpenAPI paths. They do not require PostgreSQL or a browser, and do not prove GIS SQL, database concurrency, or cross-origin cookie behavior.

`src/GeoSearch.Web.Search.Tests` contains additional Moq-based controller validation and application-service mapping tests. It is excluded from the solution until `Moq` 4.21.0 and transitive dependencies are supplied for offline restore; those tests have **not** been compiled or run in this workspace.

## Runtime verification

Use the [devcontainer database](../.devcontainer/README.md) for the following checks:

- **Persistence:** apply migrations to a disposable `geosearch_auth` database; verify failed-login persistence, refresh rotation, token-family replay revocation, logout, password-change invalidation, and concurrent refresh behavior.
- **Cleanup:** verify that only records older than expiry plus retention are pruned, including consumed records from active families; verify that database outages do not stop API startup and that pruned tokens remain unauthorized.
- **Browser security:** verify HTTPS, Secure and SameSite cookies, the `X-CSRF-TOKEN` exchange, CORS, reload/session restoration, and multi-tab refresh. Check both normal container configuration and the optional Windows Visual Studio origin when applicable.
- **Development environment:** build/reopen the devcontainer, execute the `just` import recipes against PostGIS, and confirm that Docker host-port publication does not occupy Visual Studio's application ports.

These are integration checks to perform on the running development stack; automated unit tests are not substitutes for them.

## GIS runtime acceptance checks

Use a restricted `geosearch_reader` login with only the permissions described in [development](development.md#configure-the-server). Attempt an administrative write to prove that the database rejects it; the application's EF read-only guard alone is insufficient. Run `just s05-search-index` after the data imports to create both the indexed view and the search/reverse SQL functions.

Verify `/api/search`, `/api/reverse`, `/api/nearby`, and `/api/categories` against the populated database. Cover required/malformed query parameters, boundary coordinate values and radius/limit values, empty results with snapshot metadata, longitude/latitude axis order, category labels, nearest-result behavior, and distance-in-metres ordering. Confirm that a refresh changes the exposed snapshot only as the materialized view changes and that coordinates are not present in API logs. Verify the API is usable without Identity tokens.

Collect the instructor's ten fixture queries with their actual top result and measured latency, including the three known ranking errors described in [the lab specification](../lab/lab-2.md#test-your-search-with-ten-fixture-queries). The target is a search response under 300 ms on the Hannover extract; offline unit tests cannot establish this. Keep the future frontend acceptance checks separate: map markers, attribution, permission denial, stale-request cancellation, location accuracy circle, map-centre fallback, and a screenshot of nearby search using the device position.
