# GeoSearch UI conventions and visual identity

The client uses MudBlazor for accessible controls and a restrained cartographic visual identity: mineral-ink text, evergreen accents, light surfaces, subtle contours, and compact information hierarchy. Custom styling complements MudBlazor rather than replacing its component behavior or accessibility support.

## Component responsibilities

Razor views declare the layout, component bindings, accessibility metadata, and routes. Behavior, mutable state, asynchronous workflows, lifecycle management, and navigation belong in matching `.razor.cs` partial classes. Markup-only components do not need code-behind files until they acquire behavior.

Reusable markup lives in `UI/Components`. A component owns one meaningful unit of UI behavior and styling; avoid splitting an ordinary page into incidental helper files. Keep API interactions in dedicated services when the search UI is implemented, with code-behind orchestrating the view instead of duplicating business logic.

## Styles and theme

- `Services/Theming/GeoSearchMudTheme.cs` owns MudBlazor palette, typography, radii, layout measurements, and elevation.
- `UI/Styles/core/_tokens.scss` holds complementary CSS variables for custom surfaces and cartographic motifs.
- `UI/Styles/core/_global.scss` contains global resets and intentional application-wide adjustments to MudBlazor elements.
- `UI/Styles/app.scss` imports shared styles and custom component SCSS files.
- Adjacent `Page.razor.scss` files compile to CSS-isolated `Page.razor.css` and contain only page-specific styling. Shared styles use component-specific classes to avoid global collisions.

Write source SCSS in expanded form: four-space indentation, one property declaration per line, and separate blocks for nested selectors and media queries. Minification belongs to build artifacts, never source files. SassCompiler produces generated CSS during the .NET build; do not edit those outputs.

## Design tokens

| Role | Light value | Purpose |
| --- | --- | --- |
| Primary | `#117A68` | Actions, active navigation, map emphasis |
| Ink | `#192C32` | Headings and high-contrast text |
| Muted text | `#61747A` | Supporting descriptions |
| Background | `#F3F7F6` | Workspace canvas |
| Surface | `#FFFFFF` | Cards, forms, app bar |
| Divider | `#DCE6E3` | Subtle visual boundaries |
| Warm accent | `#D4A153` | Secondary emphasis |

The client uses a local system sans-serif stack and monospace typography for coordinates. Dark palette values exist for a future theme switch, but the application currently starts in light mode. A user-selectable theme should be owned by a dedicated service rather than by individual pages.

## Layouts and feature boundaries

`MainLayout` supplies branding, common navigation, and a keyboard skip link. The map workspace uses a two-column arrangement that collapses on smaller viewports. App-wide MudBlazor providers appear once in `UI/App.razor`.

The map is rendered by `UI/Components/MapViewer`, which owns the MapLibre instance, browser capability check, map controls, and map-specific failure feedback. Its basemap style is selectable through the public client `Map:StyleUrl` setting, defaulting to the local `map-styles/osm-standard.json` style. Search remains disabled until F2 and result markers are deferred to F3. MapLibre-specific types must stay inside the map component; subsequent search features will supply GeoJSON-facing view models.

## Accessibility and verification

Use semantic headings, explicit form labels, keyboard-operable controls, clear error announcements, and visible focus states. Convey operational status through text or icons as well as color. Verify layouts at desktop and mobile widths in a browser; the .NET build verifies Razor and SCSS compilation but not visual correctness.
