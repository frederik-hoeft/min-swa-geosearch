# Bootstrap validation

The initial bootstrap is a locally compiled scaffold. It does not yet implement Lab 2 GIS
endpoints, browser geolocation, basemap tiles, GeoServer publishing, or a production deployment.

## Reproducible offline checks

The provided SDK archive contains .NET SDK **10.0.403** for Linux x64. The supplied NuGet ZIP
contains **121** packages. They were extracted into an isolated cache outside the Git tree;
package sources were cleared during restore. Environment variables affecting MSBuild platform
selection were unset.

The following checks passed with the supplied package set:

- Offline solution restore and compilation for .NET 10 with `GeoSearchEnableSassCompiler=false`.
- Warnings-as-errors compilation using the complete analyzer policies in `src/.editorconfig`
  and `src/.globalconfig`, including the narrow project-local exceptions.
- `dotnet format src/GeoSearch.slnx --verify-no-changes --no-restore`.
- MSTest baseline: one client and three server tests, no failures.
- Git whitespace and patch-application checks; XML/JSON and Bash syntax validation.

The supplied package set does **not** contain `AspNetCore.SassCompiler` 1.103.0.
Normal restores intentionally require that package, following the same SCSS compilation setup
as the repository's project configuration. The offline compile above excluded only the Sass
package; it did not compile or verify SCSS-generated CSS.

## Unverified behavior

A database instance with the imported OSM dataset was not available for integration tests.
The Identity and WKG transaction integration have been compiled, but no end-to-end login,
lockout persistence, EF migration, or web host startup test has been run against PostgreSQL.
The current development-only `EnsureCreatedAsync` bootstrap is intentionally restricted to
an empty Identity database. No EF migration has yet been scaffolded; generate one, review it,
and disable the temporary bootstrap before deployment. Source-generated WKG model discovery
compiles, but runtime schema/policy compatibility must still be tested.

The devcontainer definition is structurally checked but has not been built or launched.
The normal online build, Sass output, WASM application startup, MapLibre resources, GeoJSON
rendering, and browser/CORS/security flows remain deployment review gates.

As the search API is introduced, test it against the lab's ten fixtures and measure the
required latency using an imported PostGIS database. Avoid representing baseline unit tests
as evidence for that integration behavior.
