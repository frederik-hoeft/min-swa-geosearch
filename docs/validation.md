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

Test through the public HTTPS origin with a browser that trusts the local certificate. The landing page is at `/`; the interactive map workspace is at `/map`. At desktop/tablet widths and in landscape phone orientation, the search pane overlays the left of the full-size map; its subtle chevron tab below the input collapses/expands it vertically. In portrait phone viewports under 600px, the bottom sheet has its chevron tab **above** the input, expands upward to cover most of the map, and collapses to a compact bottom search bar. It is a button, not a draggable sheet.

Test especially at 320×568, 390×844, 568×320, 844×390, 768×1024, and 1440×900. Focus the now-editable search field with the pane first collapsed and then expanded: on phones the pane must temporarily cover the map, keeping the input visible above the software keyboard; typing must not call the API before F2. Tab from the search field to the pane toggle or a focusable result, and tap a result: expansion and `aria-expanded` must remain consistent until the interaction completes. Blur by tapping outside the pane and confirm it returns to its previous manual state. On desktop and tablets, focusing the input must not change the layout. Repeat in portrait and landscape, with a real on-screen keyboard on Android and iOS when available. This is distinct from checking CSS breakpoints in desktop emulation. Toggle the pane repeatedly, including during orientation changes, and verify the map camera, tiles, and canvas bounds **remain unchanged**: only the overlay should animate. Verify that OSM attribution appears at the top right below the map navigation controls and stays readable and clickable in either state. Neither state may cause horizontal or vertical document scrolling; the pointer wheel over the uncovered map should zoom without scrolling the page. Long result lists must scroll only inside the expanded pane, leaving the search input and chevron tab accessible. Check keyboard toggle operation, `aria-expanded` state, reduced-motion preferences, and visible button focus.

Check initial navigation to `/` and `/map`: focus should move to the main landmark for accessibility, without drawing an unexpected outline around the hero heading or selecting any page text. The GeoSearch logo returns to `/`, and Explore navigation stays aligned.

Search features are not yet implemented. Once available, verify search debounce (at least 300 ms after three characters), error and no-results feedback, result selection, and marker placement. For device position, test granted, denied, unavailable, and timeout outcomes. Nearby results should use the device position when permitted and the map centre otherwise, with the source position clear to the user. Check the position accuracy circle and stale-request handling if the UI issues overlapping requests.

Use a deliberately missing style URL and malformed style JSON to verify that the map shows an actionable startup error rather than an indefinite spinner; also test a blocked tile request and a failing JS module import. A style that fails after preflight must produce a bounded load-timeout message. These are browser checks, not proofs supplied by Razor compilation. Browser tests also confirm that fingerprinted Blazor files load successfully, including the `/_framework/` modules resolved through the publish-generated import map. Development-mode Swagger UI and the `/swagger/v1/swagger.json` description document the four API routes; production runtime mode should not expose them.

## Capture submission evidence

The lab requires ten observed top search results with latency measurements, one screenshot of nearby search using the device position, a short architecture note, and a five-minute demonstration. Use the [lab handoff record](lab-handoff.md) for those deliverables and the instructor's [fixture list](../lab/lab-2.md#test-your-search-with-ten-fixture-queries) as the source of expected results.

For the ten search queries, measure the same end-to-end HTTP path under comparable conditions and report the median. The target is less than 300 ms on the Hannover extract. Record the actual top result even when ranking is wrong; the lab identifies three known failures to explain. If nearby queries are slow, profile with `EXPLAIN (ANALYZE, BUFFERS)` before considering the [optional spatial index](architecture.md#privacy-and-quality-boundaries). Do not modify instructor-managed SQL or indexes without confirming the permitted scope.
