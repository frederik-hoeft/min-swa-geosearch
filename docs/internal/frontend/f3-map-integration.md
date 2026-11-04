# F3: committed search markers and known-place selection

**Status:** Implemented on the F3 review branch; live browser acceptance outstanding.

**Contract:** [F5 interaction model](f5-interaction-contract.md) · [F2–F6 backlog](backlog.md) · [UI conventions](../../ui-design.md) · [Validation](../../validation.md)

## Ownership and flow

`SearchWorkspace` remains the sole owner of committed search/inspection state and HTTP requests. `SearchMapState.FromPanel` derives a read-only projection of the committed result snapshot and optional selected known feature. Autocomplete drafts do not alter it. `Map` passes that projection to the existing `MapViewer`; neither `Map` nor `MapViewer` issues a search/reverse request in response to a known marker click.

`MapResultOverlay`, inside the map component boundary, synchronizes **HTML/CSS MapLibre markers** from the committed search projection. The Blazor.MapLibre wrapper's `AddMarker` accepts a `MarkerOptionsExtensions.HtmlContent` body, a CSS class, and a safe `OnClickAsync` callback whose payload contains only a marker type and coordinate. One marker is shown per valid feature ID and the selected marker replaces its normal counterpart. The button inside each marker provides keyboard focus, hover/focus styling, a readable label, and native click activation; the label is HTML-encoded. All appearance uses theme-aware CSS variables and no new application JS is required. Updated search results reconcile marker additions/removals; camera movement uses the managed `EaseTo` method.

Map pins select through a component-reference method (`SearchWorkspace.SelectMapFeatureAsync`) rather than a UI event originating in the search component. That method must explicitly schedule the search component's render after updating its state; a parent map update does **not** guarantee a child render with unchanged parameters. This is independently covered by an HTML-renderer regression test, including consecutive marker selections.

List and marker selections use the same `CommitPlace` transition. A place chosen **directly from autocomplete** is a new semantic choice: its pin is shown, but an unrelated previously committed query's markers are temporarily hidden. The old committed state stays available as a genuine **Back** target; returning restores its list and markers without requery. Selecting a feature **from the committed list or a map pin** continues to show the complete result set alongside the selected pin. Selecting another marker replaces the current inspection rather than nesting a new Back target. Clearing/replacing a committed query removes old results; editing an uncommitted draft leaves them intact.

On portrait phones with an expanded bottom sheet, selection uses the built-in MudBlazor viewport service and a small Web Mercator camera-center adjustment to position the pin in the uncovered upper part of the map. It does not rely on the wrapper's object-shaped camera offset, change persistent map padding, or resize MapLibre's canvas; changing the panel's collapsed state alone does not move the camera. Once a mobile input-driven search or place selection is committed, Blazor moves focus outside the input overlay via `ElementReference.FocusAsync`, dismissing the software keyboard and restoring the ordinary expanded result sheet. Desktop focus behavior is unchanged. The selected pin uses the theme's error/red palette token for stronger contrast over urban OSM imagery; the basemap and attribution remain unchanged.

## Focused automated checks

- Search map projection: draft edits preserve results; new commits and Clear remove old markers; Back retains the actual committed list; known place from autocomplete has a selected point without a fictional list; selecting multiple places preserves one return target.
- Marker projection: committed versus draft results, duplicate/invalid coordinates, single selected marker, and direct autocomplete selection. HTML encoding, keyboard-operable button, bottom anchor, and wrapper marker-options serialization.
- A component-renderer regression selects two markers through the external method and verifies actual place-details HTML changes without a parent parameter update. It fails when the explicit child render is removed.
- Existing F2 search session tests continue to cover request handoff and stale-response rejection.

## Required browser acceptance

Run with a populated search API and verify at desktop, portrait phone, and landscape phone sizes:

1. Submit a literal search and confirm all committed results appear at their correct map coordinates. Editing another draft leaves them unchanged; committing a different query or Clear removes the old markers. Show more, empty results, and network failures do not display stale markers.
2. Hover or keyboard-focus a rendered pin: only that button should show the pointer/focus styling. Click the regular and selected pins on the map, on desktop and touch screens. Each opens the matching known-feature details and highlights the correct pin; camera navigation follows the selection without issuing another `/api/search` or `/api/reverse` request. Check an ordinary map drag/double-click still pans/zooms.
3. Back restores the genuine prior result set. With query A committed, start editing and choose a place directly from query B's autocomplete: query A markers should disappear while that place is inspected, but Back should restore query A markers. Choosing a feature from query A's **result list** must keep the other query A markers visible. Repeated selections do not stack inspection panels.
4. On portrait phones with the bottom sheet expanded, selecting a list result positions its pin in the unobstructed upper map area. The selected red pin stays distinguishable against dense OSM city tiles. Collapsing/expanding the sheet alone leaves the map/camera unchanged. Verify light/dark themes, visible OSM attribution, map navigation, and no document-level scrolling.
5. On an actual phone, type a query with the on-screen keyboard and submit with the keyboard's Search/Enter action. Focus must leave the search input, the keyboard must close, the full-screen focus overlay must revert to the normal expanded results sheet, and the map must be visible above it. Also check selecting a suggested place and confirming by tapping; desktop focus should not jump.
6. Verify the slimmer SVG/CSS-backed pin tips mark the feature coordinates and that selected markers are visually distinct in light and dark themes. Confirm no MapLibre source/layer or circular JSON errors appear in the console, and that navigating away/back recreates one map without stale markers. The upstream wrapper retains per-marker callback references until map disposal; monitor behavior during many repeated searches.

These browser/WebGL checks are **not** covered by the managed unit tests and remain a review gate. F4 will handle background-map reverse clicks in the same contextual panel and must distinguish them from clicks on known DOM markers.

**Wrapper limitation:** The generic `MapLibre.AddAsyncListener("error", ...)` event bridge uses `JSON.stringify(event)`, which can throw on circular MapLibre error payloads. We intentionally do not register that unsafe listener. Explicit marker interop errors, style preflight failures, and the initial load watchdog remain visible. Detailed post-load tile errors require an upstream wrapper fix that serializes a sanitized error payload; do not reintroduce the generic error listener.
