# Validation and integration boundaries

GeoSearch's initial implementation is an application foundation, not a completed GIS submission. The map is a placeholder; search, nearby/reverse GeoJSON endpoints, browser geolocation, and a local basemap are outside the current implementation.

## Automated checks

Build and test the solution with the .NET 10 SDK:

```bash
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
```

The GitHub Actions workflow performs restore, formatting verification, a Release build with warnings treated as errors, and tests. Local builds also compile the SCSS sources through SassCompiler.

The unit tests cover Identity/JWT configuration, refresh-token model and migration metadata, browser authentication HTTP contracts, record-constructor validation, API-only antiforgery middleware, and refresh-token cleanup cutoffs. These tests do not require a database server.

For an isolated/offline build, see [the offline dependency procedure](development.md#offline-dependency-bundle). The supplied test archive contains .NET SDK 10.0.403 and an offline NuGet package set; SassCompiler's Dart executable needs executable permission when the package is unpacked manually on Linux.

## Runtime verification

Use the [preferred devcontainer environment](development.md#preferred-development-environment) and PostgreSQL to exercise the application after changing authentication, migrations, or setup scripts. The following checks require real infrastructure rather than unit tests:

- Apply EF migrations to a disposable `geosearch_auth` database; exercise failed-login persistence, login, refresh rotation, expired/replayed-token family revocation, password-change session invalidation, and logout under concurrent requests.
- Confirm that the pruning service deletes only tokens older than expiry plus retention, including consumed tokens in a still-active family, and that a database outage does not prevent API startup. An expired-and-pruned token must remain unauthorized.
- Verify HTTPS development certificates, secure cookies, credentialed CORS, antiforgery validation, browser reload/session restoration, multi-tab refresh, and the Development-only client API-origin override.
- Check Dev Containers startup and Docker port publication. Visual Studio on the host is an optional debugging environment; its 5100/5200 ports must remain available when the devcontainer is running.

No PostgreSQL or browser end-to-end test was run as part of the isolated .NET validation. In particular, compilation and mock-host tests do not prove actual token retention, rotation races, database migrations, or cookie behavior in deployed browsers.

When the GIS feature work begins, import the instructor's dated OSM snapshot, exercise the ten supplied search fixtures, validate the GeoJSON contract and coordinates, and measure the required median search latency against real PostGIS data. Keep this performance validation independent of authentication unit tests.
