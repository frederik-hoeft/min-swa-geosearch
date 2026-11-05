# F4: Background-map reverse inspection

**Status:** Implemented for review; full WebGL/browser acceptance remains outstanding.

**Contract:** [F5 interaction model](f5-interaction-contract.md) · [Frontend backlog](backlog.md) · [F3 map integration](f3-map-integration.md)

## Scope and ownership

Clicking an empty part of the MapLibre map requests `GET /api/reverse?lat=...&lon=...&radius=250`. The map sends a **named WGS84 coordinate** to `SearchWorkspace`, which owns the reverse request and the typed `ReverseInspectionPanel` state. The existing `SearchContextPanel` shows the clicked latitude/longitude, loading, nearest feature and its distance **from the clicked point**, no result within 250 m, or a retryable API error. There is no popup and no separate full results view.

`SearchMapState` projects existing committed forward results and known-place selections from underneath reverse inspection. A distinct noninteractive ring-and-dot marker is rendered at the clicked point through the same `MapResultOverlay` that manages normal HTML markers. The marker uses the theme primary fill with the surface color as its outline and center dot, so it stays visually consistent with light and dark themes. The inspection marker is not automatically moved to the nearest returned feature. Existing discovery markers and the text field's committed selection remain unchanged. `Back` and `Escape` restore the prior panel and remove the inspection marker. Repeated map clicks replace the foreground inspection instead of nesting Back targets. Selecting a known result marker calls the existing feature-selection path; the wrapper's marker handler stops the click from bubbling into a background reverse request.

Reverse requests are independent of live autocomplete and committed forward searches. A request generation plus cancellation prevents stale *successes and failures* from overwriting newer inspection or search actions, including after Back or component disposal. A forward query already in progress can still populate the preserved return target while inspection is open. No network calls occur from ordinary panning or zooming. The implementation uses the MapLibre wrapper's managed `AddAsyncListener<JsonElement>("click", ...)` API with no application-specific JavaScript additions.

**Deferred to F5:** A clicked coordinate can later be offered as the origin for **Search nearby here**, but no inert action is surfaced before the nearby editor exists.

## Browser acceptance

1. Search for a term, confirm a list and markers, then click empty map background. One `/api/reverse` request uses the clicked `lat`/`lon` and 250 m radius; the contextual panel shows **Map inspection**, not a popup. The original result markers remain; the inspection marker stays at the clicked coordinate.
2. Verify successful nearest address/POI (with `distance_m`), empty result, API 400/500 or network failure, and Retry. The returned feature's coordinate must not be represented as the clicked point.
3. Click A then B rapidly and delay A's response: the details and inspection marker must refer only to B, even when A later fails. Back or Escape must restore the original committed list, selected feature, and field text without another forward request.
4. Click a known search marker or select a list result: no reverse HTTP request; the known place's details and map highlighting remain correct. Repeated background clicks must not stack Back screens.
5. On desktop and portrait/landscape phones, the panel opens enough to show inspection, its internal content can scroll, the map remains full-size, and OSM attribution remains visible. Check light/dark themes, keyboard Escape and Back, and map pan/zoom without unsolicited reverse requests.
6. Navigate away while a reverse request is pending and back again: no abandoned request may update the new page. Confirm no circular JSON serialization errors in the console for ordinary map clicks; the generic MapLibre `error` listener remains intentionally disabled due to the upstream wrapper's unsafe error-event serialization.

These checks require a live browser/MapLibre environment. The managed regression tests cover request URI encoding, GeoJSON distance semantics, state transitions, out-of-order completions, and renderer-level dismissal/selection behavior; they do not prove actual canvas hit testing or touch interaction.
