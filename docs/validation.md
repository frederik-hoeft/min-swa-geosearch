# Validation and integration boundaries

The initial GeoSearch implementation is an application foundation. Authentication, database ownership boundaries, and the browser shell are implemented; the GeoJSON search endpoints, interactive map, and geolocation features are pending. Validation therefore distinguishes automated scaffold checks from tests requiring a live browser and PostGIS instance.

## Automated validation

From the repository root in the [preferred devcontainer](development.md#start-the-devcontainer-and-prepare-the-databases):

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

GitHub Actions restores dependencies, checks formatting, builds in Release with warnings treated as errors, and runs tests. The build also compiles the client SCSS through SassCompiler.

The automated tests exercise Identity/JWT configuration, refresh-token mapping and EF migration metadata, browser session request contracts, record-constructor validation, API-only antiforgery middleware, and refresh-token cleanup boundaries. These tests do not require PostgreSQL or a browser, and do not prove database concurrency or cross-origin cookie behavior.

## Runtime verification

Use the [devcontainer database](../.devcontainer/README.md) for the following checks:

- **Persistence:** apply migrations to a disposable `geosearch_auth` database; verify failed-login persistence, refresh rotation, token-family replay revocation, logout, password-change invalidation, and concurrent refresh behavior.
- **Cleanup:** verify that only records older than expiry plus retention are pruned, including consumed records from active families; verify that database outages do not stop API startup and that pruned tokens remain unauthorized.
- **Browser security:** verify HTTPS, Secure and SameSite cookies, the `X-CSRF-TOKEN` exchange, CORS, reload/session restoration, and multi-tab refresh. Check both normal container configuration and the optional Windows Visual Studio origin when applicable.
- **Development environment:** build/reopen the devcontainer, execute the `just` import recipes against PostGIS, and confirm that Docker host-port publication does not occupy Visual Studio's application ports.

These are integration checks to perform on the running development stack; automated unit tests are not substitutes for them.

## GIS acceptance criteria (future implementation)

After the spatial API and client have been implemented, load the dated Geofabrik snapshot through the instructor's `just` recipes. Validate `/api/search`, `/api/reverse`, `/api/nearby`, and `/api/categories` against the lab's fixtures, including GeoJSON structure, longitude/latitude order, category and distance behavior, and failure responses. Measure the required median search latency below 300 ms over ten fixture queries on real PostGIS data.

Browser tests should exercise map updates, geolocation permission denial, cancellation of obsolete requests, and the absence of third-party geocoding. The initial styled map workspace is explicitly not evidence of these capabilities.
