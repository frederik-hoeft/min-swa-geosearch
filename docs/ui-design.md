# GeoSearch UI conventions and visual identity

The client uses MudBlazor for accessible controls and a restrained cartographic visual identity: mineral-ink text, evergreen accents, light surfaces, subtle contours, and compact information hierarchy. Custom styling complements MudBlazor rather than replacing its component behavior or accessibility support.

## Component responsibilities

Razor views declare the layout, component bindings, accessibility metadata, and routes. Behavior, mutable state, asynchronous workflows, lifecycle management, and navigation belong in matching `.razor.cs` partial classes. Markup-only components do not need code-behind files until they acquire behavior.

Reusable markup lives in `UI/Components`. A component owns one meaningful unit of UI behavior and styling; avoid splitting an ordinary page into incidental helper files. Keep API interactions in dedicated services when the search UI is implemented, with code-behind orchestrating the view instead of duplicating business logic.

## Styles and theme

- `Services/Theming/GeoSearchMudTheme.cs` owns MudBlazor palette, typography, radii, layout measurements, and elevation.
- `UI/Styles/core/_tokens.scss` holds complementary CSS variables for custom surfaces and cartographic motifs.
- `UI/Styles/core/_breakpoints.scss` centralizes compile-time Sass breakpoint variables. Reference them with `@use "../Styles/core/breakpoints" as bp;` in page/component/layout SCSS; CSS variables cannot be interpolated into media queries. The phone/tablet boundaries follow MudBlazor's 600px/960px transitions, with additional named thresholds for specific layouts.
- `UI/Styles/core/_global.scss` contains global resets and intentional application-wide adjustments to MudBlazor elements.
- `UI/Styles/app.scss` imports shared styles and custom component SCSS files.
- Adjacent `Page.razor.scss` files compile to CSS-isolated `Page.razor.css` and contain only page-specific styling. Shared styles use component-specific classes to avoid global collisions.

Write source SCSS in expanded form: four-space indentation, one property declaration per line, and separate blocks for nested selectors and media queries. Minification belongs to build artifacts, never source files. SassCompiler produces generated CSS during the .NET build; do not edit those outputs.

## Design tokens

| Role | Light value | Dark value | Purpose |
| --- | --- | --- | --- |
| Primary | `#117A68` | `#69D3B4` | Actions, active navigation, map emphasis |
| Ink | `#192C32` | `#EBF5F2` | Headings and high-contrast text |
| Muted text | `#61747A` | `#AFC5C4` | Supporting descriptions |
| Background | `#F3F7F6` | `#101E24` | Workspace canvas |
| Surface | `#FFFFFF` | `#1D3036` | Cards, forms, app bar |
| Divider | `#DCE6E3` | `#355056` | Subtle visual boundaries |
| Warm accent | `#D4A153` | `#E2B86F` | Secondary emphasis |

The client uses a local system sans-serif stack and monospace typography for coordinates. `ThemePreferenceService` owns the light/dark preference and synchronizes it with the application-wide `MudThemeProvider`. The app follows the browser's color scheme until the user selects a theme with the header toggle. That selection is persisted in browser local storage and takes precedence over subsequent system changes; if storage is unavailable, the toggle remains usable for the current session. Custom SCSS tokens derive their colors from the active MudBlazor palette so that the landing cards, navigation, and map workspace match the selected theme.

`ThemeHost` connects this preference to `MudThemeProvider` and holds the main UI behind an opaque loading surface until the saved preference and current system scheme are known. Its browser `prefers-color-scheme` observer only reports changes; it never mutates MudBlazor's internal state, so an explicit user selection remains authoritative. The observer is released when the service is disposed.

The OSM Standard raster basemap retains its published colors in both modes; theme switching affects application chrome and map notices, not third-party map tiles or attribution.

## Layouts and feature boundaries

`MainLayout` serves ordinary pages, including the landing page at `/`. `MapLayout` serves `/map` with a viewport-bound content area. Both use `AppHeader` for branding, an Explore link, and the keyboard skip link. The GeoSearch logo links back to the landing page; a separate Home navigation item is unnecessary. The app-wide MudBlazor providers appear once at the application root: theme management in `UI/Components/ThemeHost` and the remaining providers in `UI/App.razor`.

The map workspace uses a permanent, full-viewport MapLibre canvas. A search pane floats **over** the map and opens expanded on desktop, tablets, and landscape phones. On these viewports, it occupies the left edge and extends downward. Collapsing it retracts the content into a compact top-left search control; the thin chevron tab below the search field expands the pane again. The chevron is a button, not a drag handle, and has an accessible name and an `aria-expanded` state.

On portrait phones (under 600px wide), the pane is a bottom sheet. Expanded, it occupies most of the viewport height; collapsed, only its handle and search bar remain at the bottom. The handle is above the input and indicates upward/downward expansion. The sheet animates its height; `prefers-reduced-motion` disables the transition. Search results scroll inside the pane while the handle and search input stay reachable. On phones, focusing the search field temporarily expands the pane to cover the map, keeping the field near the top and clear of the on-screen keyboard. The manual expanded/collapsed preference is preserved: the temporary interaction stays open while focus moves among controls or a pointer selects a result, and restores the preference when interaction leaves the panel. Blazor owns the effective expansion state and accessible toggle label; a small panel-local JavaScript observer reports focus/pointer activity using the same Sass-defined responsive boundaries. Compact landscape phones behave similarly, while desktop and tablets retain their usual panel behavior. Collapsed content is hidden via CSS visibility rather than an HTML `inert` attribute, so it can become accessible during temporary interaction expansion. The document itself must not scroll on `/map`; wheel input over the uncovered map should zoom.

Expanding or collapsing the pane must not resize or recenter the map: its viewport and camera stay unchanged beneath the overlay. To keep the mandatory OSM attribution visible even when the bottom sheet is expanded, MapLibre's attribution control is added first in the top-right control stack, above the navigation controls. Keep the attribution readable, linked, and outside the search overlay on all supported viewports. The map-issue link sits at bottom-right on desktop and at top-left on portrait phones so it remains visible above the bottom sheet; landscape phones keep bottom-right because the left search overlay occupies the top-left corner. Do not introduce nested map scroll containers.

The map is rendered by `UI/Components/MapViewer`, which owns the MapLibre instance, browser capability check, map controls, and map-specific failure feedback. Before mounting MapLibre it validates the configured HTTP(S) style resource (which avoids silently hanging on a missing or malformed style). A bounded load watchdog covers errors that occur before the wrapper exposes its style-load event; subsequent source/tile errors use the wrapper's error listener. Its basemap style is selectable through the public client `Map:StyleUrl` setting, defaulting to the local `map-styles/osm-standard.json` style. F2 uses a typed Blazor combobox adapter in the search field. It sends debounced autocomplete requests, reuses the matching in-flight result task on literal-query commit, and renders the committed list or known-place details in the existing contextual panel. The `SearchWorkspace` UI component owns its page-local request session while `Map` owns only the full-size map, overlay geometry, and expand/collapse behavior. The `SearchSuggestionList` and `SearchContextPanel` components render suggestions and committed results/details independently; MapLibre remains independent, with markers and camera effects deferred to F3. MapLibre-specific types must stay inside the map component; subsequent search features will supply GeoJSON-facing view models.

The landing page takes visual cues from the repository's original pre-authentication intro: deep evergreen surfaces, subtle topographic contours, and open-geodata labels. It is a public index, not an authentication screen; the illustrative cartographic element is decorative and is not an interactive or data-backed map.

## Unified discovery interaction (F2–F6)

The [frontend interaction contract](internal/frontend/f5-interaction-contract.md) governs behavior across **F2–F6**, even though its name reflects the F5 design exercise. Keep one search field, typed autocomplete suggestions (draft), and one contextual panel (committed discovery or foreground inspection), rather than separate free-text/nearby/reverse tabs or a reverse popup. Typing changes suggestions, not the committed results. A selected literal query adopts the autocomplete request/snapshot; category selection opens an explicit nearby draft; a background-map click temporarily shows reverse details in the same panel with Back. Map selection, marker overlays, and camera effects derive from typed state and never drive query execution merely because the viewport moved.

On mobile the existing keyboard-driven temporary full-height expansion supports autocomplete, while committed details can use the ordinary collapsible bottom sheet so the map remains visible. The exact autocomplete overlay treatment, MudBlazor combobox adapter, selected-place detail fields, and manual **Search this area** prompt placement require the F5 prototype described in [section 10](internal/frontend/f5-interaction-contract.md#10-decisions-to-validate-in-an-f5-prototype). Do not change F1's map/overlay ownership to implement the new search flow.

## F2 autocomplete and committed panel

The search input uses a small Blazor-managed ARIA combobox adapter rather than coupling MudAutocomplete text coercion and cancellation to discovery state. A typed literal action is followed by at most three preview places from the same `limit=10` API response; committed results display the full response in the existing scrollable panel. Draft edits, Enter/Escape, highlighted option selection, explicit Clear, and place Back navigation operate on immutable search state; component-local request ownership guards late success and failure. Confirming a literal query or known place also expands a manually collapsed pane, but ordinary keystrokes do not. API errors and response parsing live in the injected `Services/Search/SearchApiClient`; the UI session remains per-component rather than using an application-wide WASM DI scope. The reusable `GeoSearch.Web.Shared.Web` URI helpers construct encoded queries and path segments, avoiding manual query interpolation. Search state and new controls use theme variables (`--gs-*` and `--mud-palette-*`) rather than fixed light-theme colors. Styling is organized in component-local SCSS modules imported by the application stylesheet, while map-overlay geometry remains page-local CSS isolation. There is no additional JS interop for F2.

Category/all-categories options are deliberately not displayed before F5 can open their actual nearby editor. The category API boundary exists for that phase; the F2 interface must not imply a nearby-search action that cannot yet complete. Result markers and map-camera selection begin in F3.

## Accessibility and verification

Use semantic headings, explicit form labels, keyboard-operable controls, clear error announcements, and visible focus states. On route changes, Blazor focuses the `main` landmark (not the page heading); only the programmatically focused landmark suppresses its browser outline, while buttons and fields retain visible focus. Convey operational status through text or icons as well as color. Verify layouts at desktop and mobile widths in a browser; the .NET build verifies Razor and SCSS compilation but not visual correctness.
