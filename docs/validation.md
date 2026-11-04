# Testing and acceptance

GeoSearch's automated tests check application contracts without importing the Hannover dataset. Acceptance testing uses a populated PostGIS database and the browser-facing HTTPS deployment. The distinction matters: passing unit tests does not establish search quality, location behavior, or the lab's latency target.

## Automated checks

Run these from the repository root in the [devcontainer](../.devcontainer/README.md):

```sh
dotnet restore src/GeoSearch.slnx
dotnet format src/GeoSearch.slnx --no-restore --verify-no-changes
dotnet build src/GeoSearch.slnx --no-restore -warnaserror
dotnet test src/GeoSearch.slnx --no-restore --no-build
python3 -m unittest discover -s scripts/tests -v
python3 -m unittest discover -s deploy/tests -v
```

The .NET tests cover GeoJSON serialization and axis ordering, HTTP parameter validation, application-service result mapping, model discovery, read-only query functions, and OpenAPI route generation. The Python suites exercise data-download and provisioning scripts using local fixtures, without fetching Geofabrik datasets or starting PostgreSQL. GitHub Actions runs lab script checks, .NET build/formatting, and .NET tests in separate jobs; the deployment Python tests can also be run locally.

Passing these checks establishes the relevant in-process behavior. It does not verify the SQL against PostGIS, permissions on a real database role, Nginx-to-API routing, or browser geolocation.

## Verify the running API and database

Use the [local Docker deployment](../deploy/README.md) after the GIS provisioning script has populated `lab1`. All browser-facing requests should pass through Nginx, not through the private API port.

```sh
curl -kfsS https://127.0.0.1:8443/api/health
curl -kfsS 'https://127.0.0.1:8443/api/search?q=Limmerstr%2012%20Hannover&limit=5'
curl -kfsS 'https://127.0.0.1:8443/api/nearby?lat=52.3759&lon=9.7320&radius=500'
```

Run these from the Docker host; `localhost` inside a DooD devcontainer may be a different network namespace. The `-k` option is only for local self-signed certificate smoke tests.

Verify all four GIS endpoints with the populated snapshot, including invalid and missing parameters (HTTP 400), empty results, stable snapshot attribution, category filters, nearest-result ordering, and metre distances. Check that GeoJSON point coordinates use longitude before latitude. Confirm that GIS endpoints require no HTTP authentication and that location coordinates are not written to request logs.

Separately, verify the `geosearch_reader` database login can call the three API read functions and query category/snapshot metadata, but cannot insert into `osm.snapshot` or change the search index. The API's read-only EF model is not a substitute for PostgreSQL privileges. Confirm that an index refresh updates the snapshot metadata observed by the API.

## Browser acceptance

Test through the public HTTPS origin with a browser that trusts the local certificate. The landing page is at `/`; the interactive map workspace is at `/map`. At desktop and narrow/mobile viewport sizes, verify that the map and permanent left search sidebar fit within the viewport without document-level horizontal or vertical scrollbars. The pointer wheel over the map must zoom without scrolling the page; the search pane may scroll internally as content grows. Verify that Home/Explore navigation, keyboard focus, map resizing, and OSM attribution remain usable.

Search features are not yet implemented. Once available, verify search debounce (at least 300 ms after three characters), error and no-results feedback, result selection, and marker placement. For device position, test granted, denied, unavailable, and timeout outcomes. Nearby results should use the device position when permitted and the map centre otherwise, with the source position clear to the user. Check the position accuracy circle and stale-request handling if the UI issues overlapping requests.

Browser tests also confirm that fingerprinted Blazor files load successfully, including the `/_framework/` modules resolved through the publish-generated import map. Development-mode Swagger UI and the `/swagger/v1/swagger.json` description document the four API routes; production runtime mode should not expose them.

## Capture submission evidence

The lab requires ten observed top search results with latency measurements, one screenshot of nearby search using the device position, a short architecture note, and a five-minute demonstration. Use the [lab handoff record](lab-handoff.md) for those deliverables and the instructor's [fixture list](../lab/lab-2.md#test-your-search-with-ten-fixture-queries) as the source of expected results.

For the ten search queries, measure the same end-to-end HTTP path under comparable conditions and report the median. The target is less than 300 ms on the Hannover extract. Record the actual top result even when ranking is wrong; the lab identifies three known failures to explain. If nearby queries are slow, profile with `EXPLAIN (ANALYZE, BUFFERS)` before considering the [optional spatial index](architecture.md#privacy-and-quality-boundaries). Do not modify instructor-managed SQL or indexes without confirming the permitted scope.
