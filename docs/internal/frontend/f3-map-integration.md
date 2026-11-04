# F3: committed search markers and known-place selection

**Status:** Implemented on the F3 review branch; live browser acceptance outstanding.

**Contract:** [F5 interaction model](f5-interaction-contract.md) · [F2–F6 backlog](backlog.md) · [UI conventions](../../ui-design.md) · [Validation](../../validation.md)

## Ownership and flow

`SearchWorkspace` remains the sole owner of committed search/inspection state and HTTP requests. `SearchMapState.FromPanel` derives a read-only projection of the committed result snapshot and optional selected known feature. Autocomplete drafts do not alter it. `Map` passes that projection to the existing `MapViewer`; neither `Map` nor `MapViewer` issues a search/reverse request in response to a known marker click.

`MapResultOverlay`, inside the map component boundary, creates two GeoJSON sources and two circle layers **once per MapLibre map instance**: ordinary results and the selected feature. It updates source data when committed results/selection change, excludes a selected point from the ordinary source to prevent duplicate click events, and resolves layer clicks by stable feature ID to known GeoJSON objects. The wrapper's managed `OnClick`, `SetSourceData`, and `EaseTo` APIs are used. There are no additional app-specific JS modules, callbacks per marker, or independently owned registries.

List and marker selections use the same `CommitPlace` transition. A place chosen directly from autocomplete can be shown as a selected map point without claiming it belongs to a committed list. The **Back** action returns to the real underlying panel/results, without a requery; selecting another marker replaces the current inspection rather than nesting a new Back target. Clearing/replacing a committed query removes old results; editing an uncommitted draft leaves them intact.

On portrait phones with an expanded bottom sheet, a selection uses the built-in MudBlazor viewport service to position the selected marker in the uncovered upper portion of the map. The map canvas is not resized; changing the panel's collapsed state alone does not move the camera. Map rendering uses fixed MapLibre paint values, while application SCSS continues to use shared light/dark theme tokens. The basemap remains OSM Standard with attribution.

## Focused automated checks

- Search map projection: draft edits preserve results; new commits and Clear remove old markers; Back retains the actual committed list; known place from autocomplete has a selected point without a fictional list; selecting multiple places preserves one return target.
- GeoJSON conversion: feature identity and `[longitude, latitude]`, wrapper source serialization, selected-layer exclusion, duplicate-ID and invalid-position handling.
- Existing F2 search session tests continue to cover request handoff and stale-response rejection.

## Required browser acceptance

Run with a populated search API and verify at desktop, portrait phone, and landscape phone sizes:

1. Submit a literal search and confirm all committed results appear at their correct map coordinates. Editing another draft leaves them unchanged; committing a different query or Clear removes the old markers. Show more, empty results, and network failures do not display stale markers.
2. Click a result row (including using keyboard Enter) and a map marker. Each opens the matching known-feature details and highlights the correct marker; camera navigation follows the selection without issuing another `/api/search` or `/api/reverse` request.
3. Back restores the genuine prior result set. Choosing a place directly from autocomplete shows only its own selected marker if there is no committed list. Repeated selections do not stack inspection panels.
4. On portrait phones, the selected point stays visible above the expanded bottom sheet. Collapsing/expanding the sheet alone leaves the underlying map/camera unchanged. Verify light/dark themes, visible OSM attribution, map navigation, and that page-level scrolling remains disabled.
5. Confirm no MapLibre source/layer error appears in the console, and that navigating away/back recreates one map with no duplicated listeners or stale markers.

These browser/WebGL checks are **not** covered by the managed unit tests and remain a review gate. F4 will handle background-map reverse clicks in the same contextual panel and must distinguish them from clicks on known result layers.
