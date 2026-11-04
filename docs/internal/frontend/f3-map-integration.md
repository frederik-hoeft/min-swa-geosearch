# F3: committed search markers and known-place selection

**Status:** Implemented on the F3 review branch; live browser acceptance outstanding.

**Contract:** [F5 interaction model](f5-interaction-contract.md) · [F2–F6 backlog](backlog.md) · [UI conventions](../../ui-design.md) · [Validation](../../validation.md)

## Ownership and flow

`SearchWorkspace` remains the sole owner of committed search/inspection state and HTTP requests. `SearchMapState.FromPanel` derives a read-only projection of the committed result snapshot and optional selected known feature. Autocomplete drafts do not alter it. `Map` passes that projection to the existing `MapViewer`; neither `Map` nor `MapViewer` issues a search/reverse request in response to a known marker click.

`MapResultOverlay`, inside the map component boundary, creates two GeoJSON sources and two **pin symbol layers** once per MapLibre map instance: ordinary results and the selected feature. Two local PNG pin assets are loaded through the wrapper's `AddImage` API; their SVG sources are retained for editing. The icons use a bottom anchor (the tip marks the exact coordinate) and a separate selected style. The PNGs are 2× resolution and registered with `pixelRatio: 2` to remain crisp on high-density displays. It updates source data when committed results/selection change and excludes a selected point from the ordinary source to prevent duplicate click events. Two stable layer-level click handlers resolve identities from the minimal event JSON, accommodating optional MapLibre payload properties rather than requiring a complete event-object deserialization. Layer mouse-enter/leave callbacks provide a pointer cursor only over pins. The wrapper's managed `AddImage`, `AddAsyncListener`, `SetSourceData`, and `EaseTo` APIs are used, without any additional app-specific JavaScript or callbacks/registries per marker.

List and marker selections use the same `CommitPlace` transition. A place chosen **directly from autocomplete** is a new semantic choice: its pin is shown, but an unrelated previously committed query's markers are temporarily hidden. The old committed state stays available as a genuine **Back** target; returning restores its list and markers without requery. Selecting a feature **from the committed list or a map pin** continues to show the complete result set alongside the selected pin. Selecting another marker replaces the current inspection rather than nesting a new Back target. Clearing/replacing a committed query removes old results; editing an uncommitted draft leaves them intact.

On portrait phones with an expanded bottom sheet, a selection uses the built-in MudBlazor viewport service to position the selected marker in the uncovered upper portion of the map. The map canvas is not resized; changing the panel's collapsed state alone does not move the camera. Pin artwork has map-oriented colors chosen for contrast over the unchanged OSM raster basemap, while application SCSS and the hovered-pin cursor styling use the shared light/dark theme tokens. The basemap remains OSM Standard with attribution.

## Focused automated checks

- Search map projection: draft edits preserve results; new commits and Clear remove old markers; Back retains the actual committed list; known place from autocomplete has a selected point without a fictional list; selecting multiple places preserves one return target.
- GeoJSON conversion: feature identity and `[longitude, latitude]`, wrapper source serialization, selected-layer exclusion, duplicate-ID and invalid-position handling. Pin symbol serialization and tolerant click-ID extraction from minimal browser event JSON.
- Existing F2 search session tests continue to cover request handoff and stale-response rejection.

## Required browser acceptance

Run with a populated search API and verify at desktop, portrait phone, and landscape phone sizes:

1. Submit a literal search and confirm all committed results appear at their correct map coordinates. Editing another draft leaves them unchanged; committing a different query or Clear removes the old markers. Show more, empty results, and network failures do not display stale markers.
2. Hover over a rendered pin: only that hit region should display the pointer cursor. Click the regular and selected pins on the map, on desktop and touch screens. Each opens the matching known-feature details and highlights the correct pin; camera navigation follows the selection without issuing another `/api/search` or `/api/reverse` request. Check an ordinary map drag/double-click still pans/zooms.
3. Back restores the genuine prior result set. With query A committed, start editing and choose a place directly from query B's autocomplete: query A markers should disappear while that place is inspected, but Back should restore query A markers. Choosing a feature from query A's **result list** must keep the other query A markers visible. Repeated selections do not stack inspection panels.
4. On portrait phones, the selected point stays visible above the expanded bottom sheet. Collapsing/expanding the sheet alone leaves the underlying map/camera unchanged. Verify light/dark themes, visible OSM attribution, map navigation, and that page-level scrolling remains disabled.
5. Verify both pin PNGs load (no 404/invalid-image errors), their tips mark the feature coordinates, and the selected pin is visually distinct. Confirm no MapLibre source/layer errors appear in the console, and that navigating away/back recreates one map with no duplicated listeners or stale markers.

These browser/WebGL checks are **not** covered by the managed unit tests and remain a review gate. F4 will handle background-map reverse clicks in the same contextual panel and must distinguish them from clicks on known result layers.
