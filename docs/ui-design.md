# GeoSearch UI conventions and visual identity

The Blazor client uses **MudBlazor for accessible controls**, with a small GeoSearch-specific
visual vocabulary. The look is deliberately cartographic: deep mineral ink, evergreen accents,
quiet light surfaces, subtle contour/grid motifs, and compact information hierarchy. It is not
intended to mimic the presentation of UFWeb, whose architecture and engineering conventions
remain the reference for component organization.

## Component responsibilities

- `UI/Pages/*.razor` and `UI/Layout/*.razor` declare view structure, component bindings,
  accessibility metadata, and route/layout directives. Logic, mutable state, async workflows,
  disposal and navigation live in the matching `*.razor.cs` partial class.
- Reusable markup belongs in `UI/Components`. A markup-only component does not need a code-behind
  file; add one as soon as it acquires behavior.
- Page-specific styles live beside the view as `Page.razor.scss` and compile into **CSS-isolated**
  `Page.razor.css`. Shared/custom component styles that intentionally affect multiple views live
  in `Component.scss`, imported from `UI/Styles/app.scss`.
- Global resets, shared design tokens, and deliberate MudBlazor customization belong in
  `UI/Styles/core`, not inside individual page styles. Do not create multiple conflicting
  site-wide overrides for the same MudBlazor component.
- `Services/Theming/GeoSearchMudTheme.cs` is the authoritative MudBlazor theme: palette,
  typography, corner radius, layout measurements and elevation. `UI/Styles/core/_tokens.scss`
  provides complementary CSS tokens for the non-Mud surfaces and custom graphic details.
  App-wide Mud providers are instantiated exactly once by `UI/App.razor`.

## Design tokens

| Role | Light value | Application |
| --- | --- | --- |
| Primary | `#117A68` | Actions, active navigation, map-related emphasis |
| Ink | `#192C32` | Main headings and high-contrast text |
| Muted text | `#61747A` | Descriptions, secondary navigation |
| Background | `#F3F7F6` | Workspace surroundings |
| Surface | `#FFFFFF` | Cards, forms, app bar |
| Divider | `#DCE6E3` | Delineation without heavy shadow |
| Warm accent | `#D4A153` | Reserved secondary emphasis |

Default typography uses local system sans-serif fonts; the map workspace uses a monospace
stack for coordinates. The dark palette is defined for future user-selectable theming, but
GeoSearch currently starts in light mode. Keep any future mode preference in a dedicated
client theme service rather than scattering booleans across pages.

## Layouts and feature boundaries

`LoginLayout` is a two-panel, responsive authentication experience. `MainLayout` supplies
common branding, navigation, account actions, and a keyboard skip link. The search page uses
its own scoped two-column arrangement, collapsing to one column on narrow screens.

The map surface is an **explicitly noninteractive placeholder**. The disabled search control
and the pending-map label must remain visibly disabled until real API-backed search and MapLibre
integration land. Do not render simulated search results or a basemap that suggests live data.

When implementing the real map, keep MapLibre-specific APIs inside a focused map component and
pass it GeoJSON-facing UI models. Keep authentication/session logic in existing auth services;
Razor code-behind should orchestrate, not duplicate API or token business rules.

## Accessibility and verification

Use semantic headings, explicit form labels, meaningful error alerts, and visible focus states.
Maintain keyboard operability, including Enter-to-submit and skip-to-content navigation.
CSS must not encode operational state by color alone. Use `dotnet build` to compile both Razor
and Sass, `dotnet format` for C#, and exercise the layout at desktop and mobile widths in a
browser before considering visual behavior verified.
