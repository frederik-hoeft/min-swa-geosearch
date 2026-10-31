# Bootstrap validation

The initial bootstrap is a locally compiled scaffold. It does not yet implement Lab 2 GIS
endpoints, browser geolocation, basemap tiles, GeoServer publishing, or a production deployment.

## Reproducible offline checks

The provided SDK archive contains .NET SDK **10.0.403** for Linux x64. The refreshed NuGet ZIP
contains **130** packages. They were extracted into an isolated cache outside the Git tree;
package sources were cleared during restore. Environment variables affecting MSBuild platform
selection were unset.

The following checks passed with the supplied package set:

- Offline solution restore and compilation for .NET 10 with SCSS compilation enabled.
- Warnings-as-errors compilation using the complete analyzer policies in `src/.editorconfig`
  and `src/.globalconfig`, including the narrow project-local exceptions.
- Targeted `dotnet format whitespace --verify-no-changes --no-restore` on the modified C# files.
- MSTest baseline includes client session tests, WKG model construction, and an initial migration
  consistency check that does not connect to a database.
- Git whitespace and patch-application checks; XML/JSON and Bash syntax validation.

The refreshed package set contains `AspNetCore.SassCompiler` 1.103.0; its Dart executable
required restoring the executable bit after direct ZIP extraction. This change affects the
isolated package cache only, not the repository or its package versions.

## Unverified behavior

A database instance with the imported OSM dataset was not available for integration tests.
The Identity and WKG transaction integration compile, but no end-to-end login, lockout
persistence, migration application, or web host startup has been tested against PostgreSQL.
The generated initial migration is committed and applied by API startup. The migration snapshot
is checked against the WKG-configured model without a database connection. Actual schema creation
and transactional persistence still require PostgreSQL.

The devcontainer definition is structurally checked but has not been built or launched.
WASM application startup, MapLibre resources, GeoJSON
rendering, and browser/CORS/security flows remain deployment review gates.

As the search API is introduced, test it against the lab's ten fixtures and measure the
required latency using an imported PostGIS database. Avoid representing baseline unit tests
as evidence for that integration behavior.
