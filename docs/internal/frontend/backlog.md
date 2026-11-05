# Frontend integration backlog

**Status:** Backlog approved. F1 is implemented in PR #5 with real-browser acceptance still pending; F2 is merged via PR #8; F3 is merged via PR #10; F4 is implemented on its review branch with browser acceptance pending; F5–F6 are pending. Epics define outcomes and review gates rather than a fixed implementation plan.

GeoSearch has a Blazor WebAssembly frontend with an interactive MapLibre/OSM map, a separate landing page, and a responsive search overlay. The read-only ASP.NET Core API is implemented, F2 forward search is integrated, and F3 result visualization is integrated; F4 reverse inspection is under review, and nearby integration remains pending. These epics track the remaining Lab 2 work without moving geospatial query logic into the client.

The [F5 interaction contract](f5-interaction-contract.md) is **authoritative for cross-epic behavior in F2–F6**, even where an older backlog description differs. It describes the unified search field, suggestion semantics, committed contextual panel, nearby drafts, inspection, map overlays, and state transitions. This backlog divides that behavior into reviewable deliveries; it does not redefine it. The contract's section 10 presentation/prototype questions remain open without weakening its behavioral invariants.

The [architecture](../../architecture.md) owns the backend/data-flow model, [UI conventions](../../ui-design.md) own frontend structure and styling, and [validation](../../validation.md) and [Lab 2](../../../lab/lab-2.md) own the established contracts and acceptance requirements. This backlog records frontend phase status and the remaining integration work.

## Integration boundaries

- The Blazor client obtains search, reverse, nearby, and category data exclusively through `/api/search`, `/api/reverse`, `/api/nearby`, and `/api/categories`. It never queries PostGIS, GeoServer, or an external geocoder. Loading *basemap imagery* from an appropriately licensed tile service is distinct from geocoding and requires its own provider-policy review.
- The existing shared GeoJSON contracts are the transport boundary. Presentation-specific state and map-library objects remain client-side; MapLibre types and JavaScript interop should stay behind one cohesive map-component boundary, rather than leaking into API services or the shared project.
- UI components own presentation and interaction; dedicated client services own HTTP calls, errors, and request lifetimes. Razor markup and `.razor.cs` code-behind follow the existing UI conventions; avoid an all-purpose page component or speculative abstractions.
- Coordinates in GeoJSON are `[longitude, latitude]` (WGS 84); API parameters are `lat`/`lon` and distances/radii are metres. Never silently interchange them.
- Preserve the existing single-origin HTTPS deployment contract. Externally fetched tiles and styles must work under HTTPS and show the required attribution. Do not add database credentials or sensitive configuration to browser assets.
- Provide **one** unified search field and contextual panel, with an autocomplete draft overlay that does not replace committed content until an explicit typed selection. Do not introduce independent Search/Nearby/Reverse tabs, concurrent full result panels, a separate reverse-details popup, or freely composable free-text/nearby category filters.
- Keep immutable, typed state for autocomplete, committed discovery/nearby criteria, foreground inspection, panel presentation, viewport, and device location. Transitions are pure; HTTP, debouncing, geolocation, and MapLibre camera commands are separate effects. Build these domains incrementally, not as one giant state machine or unrelated booleans.
- For overlapping asynchronous operations, use request identity/revision and intent ownership to reject stale successes **and failures**. The autocomplete request/session owns the only debounced forward search and can transfer its snapshot or in-flight request to a committed literal query without duplicate HTTP. Cancellation is best effort, not the correctness mechanism.
- A map pan/zoom never initiates searches or changes applied nearby origins. Selected nearby criteria capture a fixed WGS84 origin at submission; map geometry is derived from state, not a second independent registry.
- Geolocation is opt-in: only a user-initiated action may request device coordinates or send them to the API. The backend must not log or store them.

## Epics and review gates

### F1. Interactive map and OSM basemap

**Implementation:** MapLibre map, configurable OSM Standard raster style, responsive search overlay, and landing-page separation implemented in PR #5. **Acceptance pending:** real-browser pan/zoom, attribution, initialization failures, and mobile keyboard behavior must still be verified. F2 has been merged via PR #8; F3 is merged via PR #10 (browser acceptance remains a runtime gate); F4 is in review and F5–F6 are pending.

**Goal:** Replace the placeholder with a usable, independently testable MapLibre map before coupling it to search.

**Scope**
- Integrate the already referenced `Community.Blazor.MapLibre` 2.0.0 wrapper into the Blazor WebAssembly page. Its stylesheet is already referenced in the client host HTML; verify the required MapLibre assets and the component lifecycle at runtime.
- Provide an interactive initial viewport centered on Hannover, with pan, zoom, responsive resizing, and appropriate controls. Retain the established page shell, theme, and keyboard accessibility.
- Evaluate the basemap choice explicitly: the familiar **OSM Standard / osm-carto** presentation is served as **raster tiles**, not an interchangeable MapLibre vector style. A MapLibre style with a raster source/layer can show it; a vector-tile provider plus a compatible style is an alternative. Configure the chosen source rather than embedding provider assumptions throughout components.
- Validate tile-provider terms, browser referrer/caching behavior, HTTPS, and attribution. The OSMF Standard tile servers are best-effort and rate-limited, not a general-purpose production CDN; no bulk prefetch or offline tile downloading.
- Handle map-load and tile-load failure visibly without a broken or misleading UI. Keep a single map instance and dispose of subscriptions/resources correctly.

**Gate F1:** A browser-loaded map supports pan/zoom, correct Hannover positioning, responsive resize, and visible attribution on the deployed origin. A failed tile request has understandable feedback. Verify WebGL2 support in the target browser, since the wrapper bundles MapLibre GL JS 6.13.0. No search/backend integration is required at this gate.

**F1 basemap decision:** Start with OSM Standard (osm-carto) raster tiles from the official OSMF HTTPS endpoint for this lab, subject to the [tile usage policy](https://operations.osmfoundation.org/policies/tiles/). Keep the map style/provider replaceable; self-hosting from OSM data remains a possible later deployment choice, not a requirement of F1.

### F2. Unified autocomplete and committed forward results

**Implementation:** One typed search combobox, debounced preview, literal query and known-place commits, reusable in-flight search snapshots, ranked result list, retry/show-more, Back/Escape/clear, and typed return targets are merged via PR #8. **Acceptance pending:** real API/browser/keyboard smoke tests. Nearby category actions are intentionally not exposed until F5 provides the editor.

**Goal:** Establish the typed draft/commit interaction and a single owner for live `/api/search` requests, without tying result display to map markers yet. This is the foundation of the [interaction contract](f5-interaction-contract.md#2-interaction-surfaces), not a continuously updated full-search panel.

**Scope**
- Keep the existing one search field. Actual user edits open an autocomplete draft; plain focus does not change the committed view or send requests. Display typed suggestions with distinct identity and accessible labels: **Search for the literal text**, **Place**, **Category / Find nearby**, and **Explore nearby / All categories**. The last two must not be exposed as dead-end actions before the F5 nearby draft/editor is available; their intent types and catalog boundary can be established now, with the completed interaction activated in F5.
- For **3–200 trimmed characters**, debounce actual user edits for **at least 300 ms**, then use `GET /api/search` for *suggestions only*. The API returns the full default snapshot (`limit=10`), while autocomplete previews just the first few, retaining server ranking. No request for shorter/overlong input; show actionable validation text. Maintain the `/api/categories` HTTP boundary for later local category matching; activate catalog loading and category suggestions with the F5 editor, not as dead-end F2 actions. Never send separate forward-search requests for categories or guess backend category values from translated labels.
- Make **literal-query selection** or unhighlighted Enter commit a forward query and its **matching** cached/in-flight result snapshot to the contextual panel. If none exists, start exactly one immediate request. Closing autocomplete must not cancel a committed query's adopted request; never relabel stale success/error as the current query. Optional **Show more** can request up to `limit=20`, but no persistent limit field is needed.
- Make **known-place selection** show details using the selected GeoJSON feature ID and payload, without another forward/reverse request. Preserve a genuine typed return target for **Back to results** (or the previous committed context when selected directly from autocomplete); do not pretend the place belongs to a list it was never in. F3 adds corresponding map camera/highlight effects.
- Distinguish actual text edits from programmatic display-label updates. Typing may refresh draft suggestions but must leave existing committed query/place/nearby state intact. Escape/outside click abandons the draft and restores the committed display text; explicit Clear resets committed discovery/selection without resetting viewport or device position. Enter on a highlighted suggestion chooses *that typed suggestion*, not the literal query.
- Keep HTTP/JSON error handling in a dedicated API boundary, UI state in typed immutable models/transitions, and side effects in an effect runner/coordinator. Exercise `MudAutocomplete<T>` interaction semantics before committing to it; use a small custom combobox adapter if its selection, text coercion, keyboard, or request-cancellation behavior conflicts with the contract. Ensure screen-reader and keyboard operation.

**Gate F2:** Typing updates only suggestions, not the prior committed panel; selecting a literal query displays a real ranked list with loading/empty/error feedback and **no duplicate request**; selecting a known feature shows its details without a requery. Tests cover snapshot adoption, Enter/Escape/clear, 3–200-character validation, selected-label editing, stale successes/failures, and the panel remaining usable when the API/map is unavailable. Category/nearby actions are activated only with their real F5 editor. See contract tests T01–T06, T09, T18–T22, and T24 for applicable phases.

### F3. Visualize committed results and inspected places on the map

**Implementation:** MapLibre-managed GeoJSON result/selection sources and circle layers, identity-based marker selection, and camera navigation are implemented on the F3 review branch, documented in [F3 map integration](f3-map-integration.md). **Acceptance pending:** real-browser interaction and responsive selection placement. No additional custom JavaScript has been introduced.

**Goal:** Synchronize the map with committed forward results and known-place inspections without making the map a second owner of search state.

**Scope**
- Render GeoJSON point markers for the **committed** forward result snapshot, not every autocomplete keystroke. Keep server result order in the panel, use stable feature identity, and handle empty/cleared/replaced queries without orphan markers or obsolete highlights.
- Selecting a committed list result or a known autocomplete place highlights the correct feature and moves the map camera to its location. Selecting a known marker reflects that selection in the contextual panel **without issuing `/api/reverse`**; do not let marker interactions also trigger background-map clicks.
- Keep place details and Back/return targets consistent: direct autocomplete place selection need not imply a committed forward-results list. A changed preview query cannot mutate committed markers; programmatic camera movement does not start a search.
- Establish one MapViewer-managed overlay interface for result markers/selection, to be extended later for reverse inspection, nearby origin/radius, and device accuracy. Do not recreate the map on each result update or let the component own domain query state.

**Gate F3:** The committed panel and markers agree on feature IDs, selection, and camera location; keyboard selection is supported; stale markers disappear on a committed query change/explicit clear, but remain while the user merely drafts a new query. A known marker does not trigger redundant reverse lookup. See contract tests T05, T07, T13, and T19 where applicable.

### F4. Map-click reverse inspection in the contextual panel

**Goal:** Inspect a clicked map coordinate through `/api/reverse` **without a separate popup or destructive change to committed discovery**.

**Scope**
- A user click on the map **background** captures named WGS84 latitude/longitude and requests `/api/reverse` (default radius **250 m**). Display the clicked location, closest returned feature, and *distance from the clicked coordinate*, or a distinct no-result/error state, **in the existing contextual panel** under a clearly labeled **Map inspection** heading.
- On a map click, dismiss any uncommitted autocomplete draft and restore the committed search-field label. Preserve the committed forward/nearby result set and criteria underneath inspection; **Back/Escape** restores that prior panel. An inspected point must not silently replace the search field's committed selection.
- Keep reverse request identity independent of the committed discovery request. Rapid clicks A then B display only B, even if A's response/error arrives last. Known marker/list selections use their existing feature, not background reverse. Clean up event subscriptions and point overlays across page teardown.
- Expose a **Search nearby here** route into the nearby draft only when the F5 editor exists; it preselects the fixed clicked coordinate and does **not** execute nearby immediately. No popups or duplicated details surfaces.

**Implementation:** Background-map clicks use the existing MapLibre click callback; a page-owned reverse request feeds a typed `ReverseInspectionPanel` in the single contextual panel. A distinct crosshair marks the clicked coordinate, and Back/Escape restore the underlying search and map markers. See the [F4 integration notes](f4-reverse-inspection.md) for architecture, limitations, and browser checks. The **Search nearby here** action is deferred until F5 can open the corresponding editor.

**Gate F4:** Background clicks produce reverse-inspection content in the same panel, with correct coordinate/distance semantics and useful loading/empty/error states; previously committed discovery survives and Back restores it. Race tests reject stale reverse responses, known markers do not reverse-geocode, and panning/zooming alone sends no request. See contract tests T07–T08 and T14.

### F5. Explicit nearby drafts, location, and shared discovery UI

**Goal:** Complete category/radius nearby discovery, device positioning, and the single contextual panel according to the [authoritative interaction contract](f5-interaction-contract.md); **no independent nearby/search modes or result panels**.

**Scope**
- Complete the unified autocomplete's **Category / Find nearby** and blank-input **Explore nearby / All categories** actions. Fetch categories from `/api/categories` and match locally (case/accent normalization if supported), using exact backend values. Category selection commits a *nearby editor/draft*, not a `/api/nearby` request. Free text and nearby remain distinct API operations, never arbitrary combined filters.
- Edit an optional category, discrete radius **250 / 500 / 1000 / 2000 m**, and typed origin **Map center / My location / Selected map point**. **Find nearby** is the explicit submit action. The applied query snapshots the origin kind and coordinate, category, and radius. Editing applied criteria changes only the draft; the old results, distances, summary, and overlays remain valid for their old criteria until the next submission; Cancel restores applied settings.
- Support **Search nearby here** from an inspected point by preselecting that fixed point in the draft, and **Search this area** after *meaningful manual* map movement while nearby results are active. Both require explicit submission/click. Search this area captures the current center at action time, retaining applied category/radius. Ordinary map movement, category selection, and editing controls never auto-query.
- Implement **Locate me** only on explicit user action in a secure context. Handle permission granted/denied/unavailable/timeout; on success display the device fix and accuracy circle and initiate the required `/api/reverse` lookup. Late fixes may update device state but cannot hijack newer manual camera/inspection intent. If My location has no fix, offer Locate me; when denied, offer **Map center** explicitly, never auto-submit a fallback nearby query.
- Present distance-ranked nearby results, applied criteria and origin labels, fixed nearby-origin/radius overlay, distinct device accuracy circle, and synchronized known-feature selection in the same contextual panel/map. Preserve one committed discovery result set and one foreground inspection; use the typed state/return-path architecture from earlier phases. A late nearby success **or failure** must not overwrite newer committed intent.
- Preserve current responsive overlay behavior: on desktop, one contextual panel and a temporary autocomplete layer; on phones, a keyboard-operable bottom sheet with a transient full-height autocomplete/keyboard interaction, manual expansion preference, and map visibility after selection. Verify focus restoration, layered Escape, Back, ARIA names/roles, and reduced-motion behavior. Prototype the six still-open presentation decisions in contract section 10 rather than reopening settled mode/origin semantics.
- Never persist/log coordinates. Internal `GeoCoordinate` names latitude/longitude; GeoJSON remains `[lon, lat]` and API queries use `lat`/`lon`; distances and radii are metres. The map alone owns map-library objects and renders overlays from state.

**Gate F5:** Demonstrate the contract's one-field/one-contextual-panel flow: autocomplete category/all-categories to editable draft, Find nearby with device/map-center/selected-point origins, fixed criteria despite subsequent pans, explicit Search this area, distance-ordered list/markers, distinct accuracy/radius overlays, reversible map inspection, and denied-location recovery. No hidden geolocation or automatic search; verify rapid intent changes and keyboard/mobile interactions. Use [T01–T24](f5-interaction-contract.md#8-acceptance-oriented-transition-and-browser-tests), emphasizing T10–T17 and T22–T24.

### F6. Refinement, acceptance, and lab handoff

**Goal:** Stabilize the integrated workflow and complete the Lab 2 evidence without expanding beyond the contract.

**Scope**
- Exercise the [contract's full 24-scenario transition/browser matrix](f5-interaction-contract.md#8-acceptance-oriented-transition-and-browser-tests), including autocomplete handoff, delayed errors, Escape/Back/clear, typed selections and genuine return targets, map click versus known-marker click, draft versus applied nearby criteria, and late geolocation/camera intent.
- Verify the unified contextual panel and current responsive overlay on desktop, tablet, portrait/landscape phones and real mobile keyboards: long labels/lists, loading/error/empty statuses, screen-reader announcements, focus order/restoration, scroll containment, panel toggling, map visibility, reduced motion, tile failure and OSM attribution.
- Add focused tests for pure typed transitions, effect ownership/generation, search-session handoff, DTO/coordinate conversion, and API-client error behavior. Check map event cleanup and reentry, permission transitions, network failure, request races, and avoidance of unintended requests. A .NET build is not a visual acceptance test.
- Confirm the Lab 2 endpoint/GeoJSON contract, user-location privacy, HTTPS restrictions, explicit map-center origin fallback and correct separation of device accuracy from nearby radius. Do not add expression syntax (`cat:` / `radius:`) or query-filter combinations unless separately approved.
- Collect ten fixture queries with observed top results and comparable measured end-to-end latency (target **<300 ms** on the Hannover extent), the required nearby/device-location screenshot, and five-minute demonstration path in the [handoff record](../../lab-handoff.md).

**Gate F6:** The [Lab 2 acceptance checklist](../../../lab/lab-2.md#acceptance-checklist) and [contract tests T01–T24](f5-interaction-contract.md#8-acceptance-oriented-transition-and-browser-tests) pass with recorded browser/deployment evidence, privacy and attribution intact, and CI/build/tests passing to the extent supported by the environment.

## Delivery and review rules

Each epic is a separate local feature branch/patch against the last **approved** baseline and ends at its review gate. F1 was implemented in PR #5; complete its remaining browser acceptance checks before closing that gate. F2 is merged via PR #8; F3 has been merged via PR #10; F4 is independently reviewable with its browser gate outstanding. Later epics receive their own implementation plans, informed by the preceding review gate and the current Blazor.MapLibre API.

Implement each phase as an independently reviewable subset of the **same** interaction contract. Foundations introduced in F2/F3 may have effects or controls completed in F4/F5, but must not expose misleading or inert user actions. The F5 contract is the source of truth if scope descriptions conflict; amendments to its behavioral invariants require explicit review. Document prototype-level presentation decisions in the contract/UI conventions when settled. Backlog acceptance criteria describe intended behavior, not functionality that already exists.

**Relevant external references:** [Blazor.MapLibre](https://github.com/Yet-another-solution/Blazor.MapLibre), [MapLibre raster source example](https://maplibre.org/maplibre-gl-js/docs/examples/add-a-raster-tile-source/), [OSMF tile usage policy](https://operations.osmfoundation.org/policies/tiles/).
