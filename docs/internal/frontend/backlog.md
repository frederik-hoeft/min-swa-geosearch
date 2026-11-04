# Frontend integration backlog

**Status:** Backlog approved. F1 is implemented in PR #5 with real-browser acceptance still pending; F2–F6 are not yet implemented. Epics define outcomes and review gates rather than a fixed implementation plan.

GeoSearch has a Blazor WebAssembly frontend with an interactive MapLibre/OSM map, a separate landing page, and a responsive search overlay. The read-only ASP.NET Core API is implemented, but frontend search, reverse, and nearby integration remain pending. These epics track the remaining Lab 2 work without moving geospatial query logic into the client.

The [architecture](../../architecture.md) owns the backend/data-flow model, [UI conventions](../../ui-design.md) own frontend structure and styling, and [validation](../../validation.md) and [Lab 2](../../../lab/lab-2.md) own the established contracts and acceptance requirements. This backlog records frontend phase status and the remaining integration work.

## Integration boundaries

- The Blazor client obtains search, reverse, nearby, and category data exclusively through `/api/search`, `/api/reverse`, `/api/nearby`, and `/api/categories`. It never queries PostGIS, GeoServer, or an external geocoder. Loading *basemap imagery* from an appropriately licensed tile service is distinct from geocoding and requires its own provider-policy review.
- The existing shared GeoJSON contracts are the transport boundary. Presentation-specific state and map-library objects remain client-side; MapLibre types and JavaScript interop should stay behind one cohesive map-component boundary, rather than leaking into API services or the shared project.
- UI components own presentation and interaction; dedicated client services own HTTP calls, errors, and request lifetimes. Razor markup and `.razor.cs` code-behind follow the existing UI conventions; avoid an all-purpose page component or speculative abstractions.
- Coordinates in GeoJSON are `[longitude, latitude]` (WGS 84); API parameters are `lat`/`lon` and distances/radii are metres. Never silently interchange them.
- Preserve the existing single-origin HTTPS deployment contract. Externally fetched tiles and styles must work under HTTPS and show the required attribution. Do not add database credentials or sensitive configuration to browser assets.
- For any overlapping asynchronous operation, prevent an older response from replacing a newer user selection or query. Switching between free-text, reverse, and nearby views must have explicit and predictable state ownership.
- Geolocation is opt-in: only a user-initiated action may request device coordinates or send them to the API. The backend must not log or store them.

## Epics and review gates

### F1. Interactive map and OSM basemap

**Implementation:** MapLibre map, configurable OSM Standard raster style, responsive search overlay, and landing-page separation implemented in PR #5. **Acceptance pending:** real-browser pan/zoom, attribution, initialization failures, and mobile keyboard behavior must still be verified. F2–F6 have not started.

**Goal:** Replace the placeholder with a usable, independently testable MapLibre map before coupling it to search.

**Scope**
- Integrate the already referenced `Community.Blazor.MapLibre` 2.0.0 wrapper into the Blazor WebAssembly page. Its stylesheet is already referenced in the client host HTML; verify the required MapLibre assets and the component lifecycle at runtime.
- Provide an interactive initial viewport centered on Hannover, with pan, zoom, responsive resizing, and appropriate controls. Retain the established page shell, theme, and keyboard accessibility.
- Evaluate the basemap choice explicitly: the familiar **OSM Standard / osm-carto** presentation is served as **raster tiles**, not an interchangeable MapLibre vector style. A MapLibre style with a raster source/layer can show it; a vector-tile provider plus a compatible style is an alternative. Configure the chosen source rather than embedding provider assumptions throughout components.
- Validate tile-provider terms, browser referrer/caching behavior, HTTPS, and attribution. The OSMF Standard tile servers are best-effort and rate-limited, not a general-purpose production CDN; no bulk prefetch or offline tile downloading.
- Handle map-load and tile-load failure visibly without a broken or misleading UI. Keep a single map instance and dispose of subscriptions/resources correctly.

**Gate F1:** A browser-loaded map supports pan/zoom, correct Hannover positioning, responsive resize, and visible attribution on the deployed origin. A failed tile request has understandable feedback. Verify WebGL2 support in the target browser, since the wrapper bundles MapLibre GL JS 6.13.0. No search/backend integration is required at this gate.

**F1 basemap decision:** Start with OSM Standard (osm-carto) raster tiles from the official OSMF HTTPS endpoint for this lab, subject to the [tile usage policy](https://operations.osmfoundation.org/policies/tiles/). Keep the map style/provider replaceable; self-hosting from OSM data remains a possible later deployment choice, not a requirement of F1.

### F2. Live free-text search and result list

**Goal:** Make the search panel useful independently of map visualization.

**Scope**
- Enable a search field backed by `GET /api/search`, with the API's existing response/validation shape. Send free text only; do not introduce separate address or town fields.
- Trigger from text changes (including typing and paste) only after at least **3 characters**, with a debounce of at least **300 ms** since the last change. Clear or reset results on shorter input.
- Show ranked result entries beneath the input, displaying each feature's readable `label` and `type`, with a sensible loading/empty/error state. Keep server ranking order and do not infer results from the map.
- Separate HTTP behavior from the page and reuse the existing shared GeoJSON models. Handle HTTP 400 `{"error":"…"}`, transport failures, rapid typing, cancellation where practical, and out-of-order completions. Avoid duplicate requests when the effective query is unchanged.
- Keep results readable and keyboard-accessible. At this stage, clicking a result may establish a selected-result state, but map effects belong to F3.

**Gate F2:** The list correctly presents real API results, displays explicit no-results/error feedback, and never issues a request below the character threshold. Rapid input changes cannot display an obsolete response. The map remains functional even if the API is unavailable.

### F3. Visualize and select search results on the map

**Goal:** Keep list and map as two synchronized views of the same search result set.

**Scope**
- Render result features as map markers using the API's GeoJSON point coordinates. Handle multiple results, empty sets, and new search queries without leaving stale markers.
- Selecting a list result visually distinguishes its marker and moves the camera to the correct position. A marker interaction should select/highlight the corresponding list entry where practical.
- Keep a stable selection by feature `id`, preserve server result order in the list, and make clear which result is active. Avoid recreating the entire map on every results update.
- Establish reusable but narrow boundaries for rendering/updating/removing search-result markers; account for later nearby markers and location markers without prematurely implementing those features.

**Gate F3:** Results visible in the list appear at their correct positions, selection is reflected in both views, camera navigation is reliable, and clearing/changing a query removes outdated markers and selection. List-driven interaction is keyboard-operable.

### F4. Map-click reverse search and popup

**Goal:** Add a deliberately small map-first search interaction and validate the reverse API end-to-end.

**Scope**
- Follow the wrapper's [click-popup example](https://github.com/Yet-another-solution/Blazor.MapLibre/blob/main/examples/Community.Blazor.MapLibre.Examples/Examples/AddPopup.razor): obtain the clicked `LngLat`, request `/api/reverse` with the clicked latitude/longitude (default radius **250 m**), and anchor a popup to that point.
- Show a loading indication, then the returned nearest address/POI/street label and distance when available; distinguish no nearby result from API failure. Sanitize/encode API-derived text before rendering into popup HTML.
- Ensure repeated clicks use the latest location rather than letting previous responses overwrite the current popup. Clean up click registrations and popup resources.
- Keep this reverse query explicitly user initiated. Investigate whether the same clicked point could become a nearby-search origin in F5, but defer that user-flow decision until the nearby UI is designed.

**Gate F4:** Clicking the map issues one correctly ordered reverse request, opens an anchored popup, and handles success, empty responses, failures, and rapid successive clicks. Neither existing search markers nor their selection are unintentionally lost.

### F5. Nearby category/radius search and device-location integration

**Goal:** Make nearby discovery intuitive without treating device location as a prerequisite.

**Scope**
- Design the interaction between free-text search, nearby mode, map selection, and result presentation. Avoid independent competing panels/marker sets with ambiguous ownership; keep a clearly indicated active mode and search origin.
- Populate the category control from `GET /api/categories` and provide radius choices of **250 / 500 / 1000 / 2000 m**. A nearby action calls `/api/nearby` with optional category, selected radius, and a valid origin; the API sorts by distance.
- Support the lab's **Locate me** action using browser geolocation only after a click, in an HTTPS/localhost secure context. Handle granted, denied, unavailable, and timed-out requests. Show the device position and accuracy circle when granted, then call `/api/reverse` as required by the lab.
- When position permission is denied or unavailable, permit nearby discovery around the **map center**. Explicitly label whether results are relative to the device or map center, including after map movement. Never run location-sensitive searches automatically merely because the map moved.
- Show nearby features both as markers and a distance-ordered list with metre distances, category/label, and synchronized selection. Reuse F2/F3 interaction patterns rather than creating a separate result-rendering architecture.
- Define how an F4 clicked position may be selected as a nearby origin, if that improves UX, without conflating it with a permissioned device location.
- Keep location state ephemeral. Do not persist or log coordinates in the frontend, URL, analytics, or server; preserve the API's non-cacheable responses.

**Gate F5:** Category and radius selections produce correct API queries. Nearby search succeeds with device location and with map-center fallback, displays results in distance order, and synchronizes list/map selection. Locate-me states and accuracy visualization are demonstrable, with the selected origin unambiguous and no automatic location transmission.

**Open F5 design questions (to resolve before implementing F5):**

1. **Search-mode ownership:** Should free-text and nearby be exclusive workspace modes sharing one result list and marker layer, or should results from both coexist? What changes when the user switches modes, and which previous query/selection is preserved?
2. **Location ownership:** Which nearby origin is active at any moment (device, map center, or F4 clicked point), how is it selected and labeled, and what happens to that origin after panning, choosing a result, or switching modes?
3. **Refresh semantics:** Which interactions explicitly trigger a new nearby request (Search button, category/radius change, origin change), and when should existing results be marked stale? Map movement alone must not silently send coordinates or trigger queries.
4. **Selection behavior:** How should the active result, map popup, origin marker, accuracy circle, and nearby-result markers interact, especially when the map is recentered by choosing a search result?
5. **Responsive layout:** Where should the mode switcher, category selector, radius selector, and location-source control live on narrow screens so the map remains usable and the origin remains clear?

**Decision for F5 planning:** Review and record answers to these questions and the resulting responsive layout/interaction model before implementation; do not prematurely hard-code them in F1–F4.

### F6. Refinement, quality, and lab handoff

**Goal:** Stabilize the integrated experience and complete lab acceptance without adding feature sprawl.

**Scope**
- Review desktop/mobile panel geometry, map resizing, overlay behavior, keyboard/focus order, touch interactions, long labels, selected states, loading skeletons, empty/error notices, and visible attribution. Align styling with MudBlazor and the existing UI design tokens.
- Exercise the combined free-text, reverse, device-location, and nearby flows, especially cancellation/races, network failure, permission transitions, invalid inputs, map teardown/reentry, and repeated mode changes.
- Confirm client behavior does not violate the API contract, data-provenance/attribution requirements, secure-context restrictions, or the location privacy boundary.
- Add focused tests for view-state transitions, API client behavior, and coordinate conversion where they provide lasting value. Verify the real browser separately; successful Razor compilation is not a visual acceptance test.
- Gather the required ten fixture queries with observed top results and measured end-to-end latency (target **< 300 ms** on the Hannover extent), a nearby/device-position screenshot, and the five-minute demo path. Update the existing [handoff record](../../lab-handoff.md) rather than duplicating submission evidence here.

**Gate F6:** The Lab 2 [acceptance checklist](../../../lab/lab-2.md#acceptance-checklist) is satisfied and documented, including attribution, privacy, denied-location fallback, useful errors, and reproducible startup. CI/build/tests pass to the extent supported by the environment, and real browser/deployment checks are recorded separately.

## Delivery and review rules

Each epic is a separate local feature branch/patch against the last **approved** baseline and ends at its review gate. F1 is implemented in PR #5; complete its remaining browser acceptance checks before closing that gate. Do not merge or begin F2 implementation before approval. Later epics receive their own implementation plans, informed by the preceding review gate and the current Blazor.MapLibre API.

Cross-phase decisions should be documented once in the appropriate architecture/UI document when settled. Backlog acceptance criteria describe intended behavior, not functionality that already exists.

**Relevant external references:** [Blazor.MapLibre](https://github.com/Yet-another-solution/Blazor.MapLibre), [MapLibre raster source example](https://maplibre.org/maplibre-gl-js/docs/examples/add-a-raster-tile-source/), [OSMF tile usage policy](https://operations.osmfoundation.org/policies/tiles/).
