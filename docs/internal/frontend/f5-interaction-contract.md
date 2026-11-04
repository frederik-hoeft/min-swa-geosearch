# F5: GeoSearch interaction contract and state model

**Status:** Authoritative interaction baseline for F2–F6 planning; prototype-level presentation decisions remain open.
**Updated:** 2026-11-04
**Scope:** Frontend F5 integration of search, nearby discovery, reverse geocoding, geolocation, map, and responsive presentation.
**Related documents:** [Frontend backlog](backlog.md) · [UI conventions](../../ui-design.md) · [Architecture](../../architecture.md) · [Lab 2](../../../lab/lab-2.md)

## 1. Purpose and design direction

GeoSearch provides **one search field**, **one contextual panel** below it, and **one map**. Users must not have to choose between independent Search, Nearby, and Reverse tabs or learn multiple query forms.

The search field is a **unified autocomplete/intent selector**:

- Typing performs *live, debounced* `/api/search` requests **for autocomplete suggestions only**. It does not continuously replace the contextual panel's contents.
- Autocomplete suggestions are typed alternatives: **search the literal text**, **choose a nearby POI category**, or **inspect a specific forward-search result**.
- Choosing any suggestion **collapses the autocomplete** and commits the corresponding contextual panel state.
- Clicking the map invokes reverse geocoding and shows its inspection details **in the same contextual panel**, not in an independent popup.
- The map visualizes results, inspected locations, device location, and committed nearby geometry; it does not own search state.

**Backend boundary:** `/api/search` accepts a single free-text query, while `/api/nearby` accepts an origin, radius, and optional category. These are separate operations, **not freely composable filters**. `/api/reverse` inspects a coordinate; `/api/categories` provides the category catalog.

The panel is the **committed view**, while autocomplete is the **draft/preview interface**. This separation is the core UX invariant.

## 2. Interaction surfaces

| Surface | Owns / presents | Does **not** do |
| --- | --- | --- |
| **Search field** | Editable text and the committed selection's human-readable text | Represent the full state solely as a string |
| **Autocomplete dropdown** | Literal-query command, offline-filtered categories, and a short preview of live `/api/search` features | Serve as a second full results panel or execute nearby searches |
| **Contextual panel** | Committed full forward-result list, selected feature details, nearby draft/results, or reverse-inspection details | Perform a second *live* forward search for every keystroke |
| **Map** | Camera, result markers, selection/highlights, committed nearby-origin/radius, inspected point, device fix/accuracy | Infer or silently mutate discovery criteria from camera movement |
| **Location control** | Explicitly obtains a device fix on **Locate me**, then initiates device reverse lookup | Auto-request permission from page load, typing, or opening nearby |

### 2.1 Typed autocomplete suggestions

Autocomplete entries have **identity and semantics**, not just display strings.

| Suggestion type | Example | On selection | Search field after selection | Contextual panel |
| --- | --- | --- | --- | --- |
| **Literal query** | `Search for "apothe"` | Commit exact typed text as forward search | `apothe` | Full results from the current `/api/search` snapshot, or loading/error/empty |
| **Category** | `Apotheke` · *Find nearby* | Commit nearby category and open its draft/editor | `Apotheke` | Origin/radius controls and **Find nearby**; after submission, nearby results |
| **Place** | `Markt-Apotheke Linden` · *Place* | Select known feature by identity | `Markt-Apotheke Linden` | That feature's details and map selection; no new `/api/search` |
| **Nearby entry point** | `Explore nearby` · *All categories* | Open nearby draft without category | `Explore nearby` or a neutral display label | Origin/radius controls; optional category |

Suggested ordering for nonempty input: literal-query action, matching categories, then the first few live place results. Use distinct icons **and text labels/subtitles** to distinguish each type. Do not imply that category-name matching is forward-search ranking.

The backend category list is fetched from `/api/categories` and filtered **locally** after loading. Matching may be case-/accent-insensitive; English synonyms and aliases are only offered if explicitly supported/mapped. The category's backend value, not a translated label guessed by the client, is used for nearby requests.

For blank input, make **Explore nearby (all categories)** discoverable without requiring users to know a category name. Ordinary focus alone must not initiate geolocation or a discovery search.

### 2.2 Autocomplete and text semantics

- **Typing** changes only the autocomplete draft and its suggestions. It does **not** commit a new discovery state or replace the contextual panel's previous committed result set.
- Only meaningful edits invoke autocomplete searching; a pure focus event does not change the committed panel.
- At **3–200 trimmed characters**, wait at least **300 ms** after the last edit before the `/api/search` request. Shorter text can show local category matches, but not a forward request.
- The literal-query option is an explicit **command**, not an additional backend result. `Enter` on ordinary unhighlighted text behaves like selecting it; `Enter` on a highlighted suggestion selects that suggestion.
- A literal query shorter than three characters is not submittable as forward search. Display the minimum-length hint; do not issue an invalid request.
- Selecting a place/category changes the field's **display text**, but must not cause `TextChanged` to fire a new semantic search. Programmatic changes and actual user edits are distinguished.
- **Escape** first closes autocomplete and abandons uncommitted edits; it does not clear an existing committed search. Clearing the search via the clear action is a separate, explicit command.
- When autocomplete closes **without selection** (Escape, outside click, map click), revert the field's uncommitted draft to the committed selection's display text, or to the empty placeholder if none exists. Do not leave uncommitted text that falsely describes the panel.
- Re-editing a selected place/category starts a **new autocomplete draft**; its prior typed semantic selection remains the committed panel state until a new choice is made or explicitly cleared.

**Selection identity is authoritative.** A label in the text field is only a presentation of `Query`, `Category`, or `Place` (or the return target after map inspection). It is never parsed to infer which one is active.

### 2.3 Single live forward-search owner

Only the **autocomplete search session** performs the debounced live `/api/search` lookup. It owns the latest query text/revision, request token, and response snapshot. Both the suggestion preview and a subsequent **literal-query commit** can refer to this same snapshot; the contextual panel does **not** independently issue duplicate requests for the same query.

- Fetch enough features for the full committed list (default `/api/search?limit=10`, not merely the 2–3 preview suggestions).
- Show a small subset of those features in autocomplete, preserving server ranking.
- Selecting **Search for "..."** promotes the *matching* snapshot to committed forward results, dismisses autocomplete, and preserves server ranking.
- If that query is still in flight, transfer/adopt its request ownership to the committed query so dropdown closure does not cancel the response needed by the panel.
- If the selected literal query has not yet been requested (for example, Enter before debounce completes), issue/reuse **one** request for that committed query instead of waiting for the debounce.
- If there is no matching current snapshot, enter committed forward **loading** and fetch it. Never promote results from a different query/revision.
- Clicking a live autocomplete **place** uses the known GeoJSON feature directly: show details, update the field to the place label, and move/highlight it on the map. It does not execute an extra forward/reverse query.
- A deliberate **Show more** from committed results may request a larger list, up to the API maximum `limit=20`. No persistent numeric `limit` form control is needed.

**Implementation implication:** The request/session owner must outlive, or safely hand off its result to, the dropdown. Treat cancellation as an optimization; explicit request tokens and query equality determine whether a completion may update state.

## 3. State domains

Use **typed immutable state**, not scattered booleans. Keep related but independently evolving concerns separate:

| Domain | What it represents |
| --- | --- |
| **Autocomplete draft/session** | Editing status, typed text, suggestion focus, pending query/revision, cached live features, and catalog availability |
| **Committed discovery** | `None`, `ForwardQuery` (query + full results lifecycle), or `Nearby` (category/origin/radius + results lifecycle); optionally a nearby draft |
| **Inspection** | `None`, a known feature, or a clicked/device coordinate with reverse-result lifecycle |
| **Panel presentation** | Which committed content is visible: discovery results, nearby editor, or temporarily overriding inspection details |
| **Viewport** | Map center/zoom and user-initiated versus programmatic camera movement |
| **Device location** | Geolocation request/permission lifecycle, last acquired coordinate, and accuracy |

**Composition, not Cartesian explosion:** A root `ExploreState` composes these domains. The contextual panel is a **projection** of committed discovery and inspection, with inspection taking visual precedence. Editing in autocomplete is an overlay; it does not destroy that underlying state.

There are **at most one committed discovery result set** and **one foreground inspection target**. An inspected result may also be a feature from a committed discovery list, but an autocomplete-selected place can be inspected without first committing a query result list.

### 3.1 Input display and panel consistency

The field may show a committed place/category/query label while the user is inspecting a **different map point**. This is acceptable **only if** the contextual panel explicitly says **Map inspection** and offers **Back**. Map inspection does not silently overwrite the committed search-field selection.

A selected autocomplete or list result normally **updates the field** to that place's label and displays its details. Keep a typed return target (the previous committed forward/nearby panel and, where available, its result snapshot/scroll position) for **Back to results**. Opening an inspection never removes previously committed results.

Do not claim that a result is part of a committed list unless it actually is. For a place chosen directly from autocomplete, an optional **Show results for "original query"** action may commit the *matching cached* query snapshot; otherwise Back returns to the true prior committed panel.

### 3.2 Draft versus applied nearby criteria

A nearby category selection prepares an **editable draft**. The draft contains:

- Optional category from the fetched catalog (`null` means **All categories**).
- Radius: one of **250, 500, 1000, 2000 metres**.
- Origin choice: **Map center**, **My location**, or **Selected map point**, as available.

Use compact discrete choices for radius; do not imply arbitrary values outside the Lab 2 contract. The controls can stay available above results or collapse into an **applied criteria summary + Edit** after submission.

**Find nearby** explicitly commits the draft and calls `/api/nearby`; choosing the category alone does not. Edits to an already applied nearby query do not mutate its results, origin, distance values, or markers until a new submission. Cancel restores the applied criteria.

A selected `Map center` is resolved to a **fixed coordinate at submission time**, not bound to future camera movements. The applied query stores both coordinate and origin kind/label. Device and selected-point origins are also captured by value.

## 4. Behavioral invariants

- **I-01: Autocomplete is the only live-search consumer.** Debounced `/api/search` requests feed suggestions; the contextual panel receives only *committed* forward results. No duplicate request for the same query merely because a user commits it.
- **I-02: Draft is not committed.** Typing, category matching, and suggestion navigation cannot implicitly replace committed discovery, inspection, or nearby results.
- **I-03: Typed selection determines behavior.** Query, category, and place suggestions are distinct, regardless of identical display text.
- **I-04: One visible contextual-panel owner.** The panel shows a committed discovery/editor or inspection details, with an explicit and predictable Back path; never two competing full result lists.
- **I-05: Search-result consistency.** Displayed counts, labels, markers, ranking, distance, category/radius summary, and errors all refer to the owning committed request. Stale data is never relabeled as current.
- **I-06: Map inspection is non-destructive.** Clicking the map can dismiss an uncommitted autocomplete draft and open reverse-inspection details, but cannot alter a committed search/nearby query.
- **I-07: Map movement is not a query.** Panning, zooming, and programmatic camera movement cannot auto-run `/api/search`, `/api/nearby`, or `/api/reverse`.
- **I-08: Nearby origin is immutable per committed query.** Distances and radius always use the submitted point, even after panning or acquiring a new device fix.
- **I-09: Nearby execution is explicit.** Category selection and edits open/change controls; only **Find nearby** or deliberate **Search this area** submit `/api/nearby`.
- **I-10: Coordinates are disclosed only through deliberate location-related actions.** No implicit device permission requests or requests on viewport change; backend must not store/log coordinates.
- **I-11: Device accuracy and query radius are different overlays.** Show both distinctly, with appropriate meanings/units.
- **I-12: Latest-request ownership wins.** Debounce, forward search, nearby, reverse lookup, and location completions can mutate state only while their typed token/intent remains current. A late *failure* is as stale as a late success.
- **I-13: Known markers do not cause redundant reverse lookup.** Selecting an already known feature uses its identity/data; a background-map click inspects the clicked coordinate.
- **I-14: One managed map.** Result markers, nearby origin/radius, inspected point, and device accuracy are independent overlays derived from state; there is no second unmanaged marker registry.
- **I-15: Failure and empty results are honest states.** No-result, API error, unavailable map tiles, and denied/unavailable/timed-out location permission have distinct feedback and retry/recovery routes.
- **I-16: Correct coordinate representation.** Internal points have named WGS84 `Latitude`/`Longitude`; GeoJSON coordinates are `[longitude, latitude]`; API query params are named `lat`/`lon`; radii/distances use metres.
- **I-17: Accessible and reversible interactions.** Autocomplete, contextual panel, mobile sheet, map actions, Back, and search controls work with keyboard and screen reader. Escape has layered, predictable behavior.

## 5. Interaction transition matrix

The table is normative for the design. **Draft** refers to autocomplete input not yet committed; **committed** refers to a selected search/query/category/place or applied nearby search.

| Given | When | Result |
| --- | --- | --- |
| Idle or any committed panel | Search field receives focus | No committed state change or backend request solely due to focus. |
| Any committed panel | User types `apothe` | Autocomplete opens; offline category matches appear; after ≥300 ms and ≥3 characters, one live `/api/search` request supplies place suggestions. Existing committed panel remains underlying and unchanged. |
| Autocomplete showing live matches | User selects **Search for "apothe"** | Collapse dropdown; promote matching result snapshot to full forward list (or load it); field displays `apothe`. |
| Autocomplete has a pending request | User commits literal query | Adopt/reuse that request, or start exactly one immediately if none exists. Dropdown collapse must not lose the needed completion. |
| Autocomplete shows a known place | User selects **Markt-Apotheke Linden** | Collapse dropdown; field displays the place name; panel shows feature details; map centers/highlights feature; no new search/reverse query. |
| Autocomplete shows category | User selects **Apotheke** | Collapse dropdown; field displays category; panel shows nearby draft with category preselected; do not query nearby yet. |
| Autocomplete opened on selected place | User edits text | Existing committed place details remain underlying; new suggestions are based on actual draft text; selecting a new option replaces the committed selection. |
| Any autocomplete draft | User presses Escape/clicks outside | Dismiss draft, restore committed field display and underlying panel; do not execute a literal query implicitly. |
| Any autocomplete draft | User clicks map background | Dismiss uncommitted dropdown/draft; start reverse inspection at clicked coordinate; underlying committed discovery is unchanged. |
| Committed forward results | User selects a list result | Show known feature details; field displays its label; map highlights/moves to feature; Back restores full results. |
| Committed selected-place details | User selects Back | Restore the actual prior committed panel/selection, without a requery if cached. |
| Forward results, nearby controls, or known-place details | User clicks map background | Reverse lookup the clicked point; show **Map inspection** details in contextual panel; underlying discovery/criteria/selection are preserved for Back. |
| Reverse map inspection | User selects Back/Escape | Dismiss inspection and restore the prior contextual panel and search-field semantics. |
| Active nearby results | User begins typing | Open autocomplete draft; do not delete nearby results or silently requery; commit a new option only when chosen. |
| Active nearby results | User changes radius/category/origin in editor | Modify draft only. Applied summary and markers remain tied to old query until Find nearby. |
| Nearby draft | User selects **Find nearby** | Snapshot full criteria and origin, collapse draft, enter nearby loading, then show distance-ordered list + radius and origin. |
| Active nearby results | User pans/zooms map | Keep origin/radius/results fixed; optionally show **Search this area** after meaningful *manual* displacement. |
| Active nearby results | User selects **Search this area** | Submit new nearby query around map center captured at click time, retaining applied category/radius. |
| Any committed panel | User clicks **Locate me** | Request geolocation; on success show device marker/accuracy, optionally center if navigation intent still current, and call reverse. Do not silently change discovery. |
| Locate me denied/unavailable/timed out | User needs nearby | Keep map and panel usable; offer explicit Map center origin; do not submit any fallback automatically. |
| Inspected map point | User selects **Search nearby here** | Prepare nearby draft with this fixed coordinate; submit only on Find nearby. |
| Any context | User explicitly clears search/selection | Clear committed query/place/category and its dependent result/selection state; return to neutral panel. Preserve viewport and device marker. |
| Any request in flight | A newer relevant intent supersedes it | Old success/error is ignored; current state cannot be overwritten by stale callbacks. |

### 5.1 Query, selection, and clearing details

1. **Searching versus committing:** live keystroke requests only refresh autocomplete suggestions. The panel is replaced only by `QuerySelected`, `CategorySelected`, `PlaceSelected`, `NearbySubmitted`, or a valid inspection action.
2. **No accidental search on selection text:** setting the input to a chosen place/category/query label is a controlled display update, not a new user edit. It must not recursively invoke `/api/search`.
3. **Pressing Enter:** when a suggestion is explicitly highlighted, choose it; otherwise commit the literal query if valid. The input is a combobox, not an unbounded text command line.
4. **Clearing versus canceling:** Escape abandons a draft; the input clear button deliberately clears the committed selection/results. Do not automatically restore a previously used nearby query after explicit clear.
5. **Return path:** when opening a place from a forward/nearby list, preserve that list and selection/scroll context for Back. When opening a place directly from autocomplete, preserve the prior committed panel plus the preview-query snapshot separately; they are not interchangeable.
6. **Panel while typing:** the existing committed panel remains the underlying state; on desktop it may remain visible beneath the dropdown, and on mobile the dropdown may cover it. The UI must visually indicate that the dropdown is *draft suggestions*, not the applied panel. Selecting/closing it resolves the temporary state.

### 5.2 Nearby origin and map interactions

- Origins are typed: `MapCenter`, `DeviceFix`, `SelectedPoint`, each captured into an immutable `GeoCoordinate` when a nearby request is submitted.
- **Find nearby** never silently requests location permission. If **My location** lacks a usable device fix, explicitly offer **Locate me**.
- **Locate me** requires a user action and a secure context (`https://` or `http://localhost`). Handle granted, denied, unavailable, and timeout. Show its accuracy circle and reverse lookup the acquired position.
- If location arrives after a newer manual map navigation/inspection, the fix may be stored/displayed, but it must not hijack the viewport or foreground inspection.
- A manual map pan does not move an applied nearby origin. **Search this area** is offered only when nearby is the active committed operation and the viewport moved meaningfully; never for free-text search.
- An ordinary map click reverse-geocodes that coordinate (default radius 250 m) and presents the nearest returned feature with its **distance from the click**, rather than claiming the clicked point and returned feature coincide.
- Clicking an existing result marker does not also trigger map-background reverse. Result selection merely updates details and highlighting.
- Device accuracy, nearby-radius outline, selected-point marker, and discovery markers remain visually distinct.

### 5.3 Result details and the contextual panel

The single contextual panel supports:

1. **Idle**: introductory guidance and optionally a nearby entry point.
2. **Forward results**: full committed list, ranked as returned, label and type, loading/empty/error feedback, optional Show more.
3. **Nearby draft and applied results**: selected category, explicit origin/radius, Find nearby, editable applied summary, distance-sorted results.
4. **Place details**: selected feature's label/type/category and map navigation, plus Back to a genuine prior state.
5. **Reverse inspection details**: clicked location, nearest known feature/distance (or no-result/error), and optional Search nearby here.

Inspection can temporarily take priority over a committed discovery panel; it does not destroy that discovery. Closing inspection restores the prior content. Do not maintain separate overlapping popup and side-panel details for the same feature.

## 6. Suggested strongly typed implementation

Prefer **immutable polymorphic state machines**, driven by explicit, typed actions, over multiple unrelated booleans (`IsSearching`, `HasNearby`, `IsInspecting`, etc.).

A minimal direction (illustrative API, not a prescribed namespace or file layout):

```csharp
public abstract record SearchSuggestion
{
    public sealed record LiteralQuery(string Text) : SearchSuggestion;
    public sealed record Category(PoiCategory Value) : SearchSuggestion;
    public sealed record Place(SearchFeature Value) : SearchSuggestion;
    public sealed record ExploreNearby : SearchSuggestion;
}

public abstract record DiscoveryState
{
    public abstract Transition<DiscoveryState> MoveNext(DiscoveryAction action);
}

public sealed record Transition<TState>(
    TState Next,
    IReadOnlyList<ExplorerEffect> Effects);

public sealed record ExploreState(
    AutocompleteState Autocomplete,
    DiscoveryState Discovery,
    InspectionState Inspection,
    DeviceLocationState DeviceLocation,
    MapViewport Viewport);
```

`DiscoveryState` might have `Idle`, `ForwardResults`, `NearbyDraft`, and `NearbyResults` variants, each nesting a typed request/result lifecycle (`Loading | Ready | Empty | Failed`) instead of creating every possible class combination. `InspectionState` distinguishes known-place inspection from coordinate inspection and maintains the typed return target. `AutocompleteState` owns a revision-tagged input draft and live lookup snapshot.

**Typed events/actions**, not UI strings, drive transitions:

```text
SearchTextEdited(text)
AutocompleteQueryDue(text, revision)
AutocompleteSearchCompleted(requestId, query, result)
SuggestionSelected(LiteralQuery | Category | Place | ExploreNearby)
NearbyDraftChanged(criteria)
NearbySubmitted(criteria)
MapBackgroundClicked(coordinate)
FeatureSelected(feature)
LocateMeRequested
GeolocationCompleted(requestId, coordinate, accuracy)
BackRequested / ClearRequested / EscapePressed
```

### 6.1 Pure transitions and effect runner

`State MoveNext(Action)` is a good abstraction for state evolution, but HTTP, debouncing, browser geolocation, and camera commands are **effects**, not responsibilities of immutable states.

```text
UI/map event
    -> typed action
    -> ExploreState.MoveNext(action)
    -> Transition(nextState, effects)
    -> effect runner: HTTP, debounce, geolocation, camera
    -> typed result action
    -> ExploreState.MoveNext(action)
```

The root coordinates cross-domain transitions; individual substate implementations own their local legal transitions. Prefer polymorphic dispatch at state boundaries and exhaustive pattern matching over typed actions. A visitor is acceptable if later justified, but avoid an action×state visitor method explosion for this limited domain.

No I/O, JS interop, rendering, timers, or mutable MapLibre objects should live inside state records. Keep API DTOs/GeoJSON parsing and view components at the boundary.

### 6.2 Async ownership and handoff

1. A text revision invalidates autocomplete responses for earlier text. Autocomplete search uses a request token, effective query, and revision to reject stale success **and** error responses.
2. Category selection supersedes pending autocomplete intent; a late forward result must not replace the nearby panel.
3. Literal-query selection **adopts** the matching autocomplete response/in-flight request. Its completion can populate committed forward results even after the dropdown is dismissed.
4. Place selection stores the selected feature directly; it does not need a new query. Setting the chosen display text does not dispatch another search.
5. New committed discovery requests supersede old discovery completions. A late nearby result cannot overwrite a newly selected forward query or place details.
6. Reverse inspection has its own request generation. A newer map click/feature selection supersedes older reverse details; it does not invalidate the unrelated discovery snapshot.
7. Device geolocation has separate request identity. A late fix can update device state but cannot steal a newer camera/inspection intent.
8. Cancellation is best effort. Generation and state-ownership checks are mandatory. Component disposal stops effects from publishing to abandoned view instances.

The cache/session may be a small coordinator service if ownership by `MudAutocomplete<T>` would otherwise cancel needed requests on dropdown closure. Keep it **page/session scoped**, not a global cross-user cache.

### 6.3 MudBlazor integration concerns

Before implementing, prototype the interaction of `MudAutocomplete<T>`'s `SearchFunc`, `TextChanged`, `ValueChanged`, item templates, debounce, and text coercion:

- Can one typed option list represent literal query/category/place entries and retain custom labels/subtitles?
- Does selecting a place programmatically set text without re-triggering live search?
- Can Enter commit the literal query while arrow-navigation/Enter selects a highlighted alternative?
- Can the committed query adopt results when closing the dropdown cancels its `SearchFunc`?
- Are keyboard focus, Escape, category selection, and accessible option names reliable on mobile and desktop?

Do not let widget defaults dictate domain semantics. If these conflict with the interaction contract, use a small custom combobox adapter while keeping the application state and transition model unchanged.

## 7. Responsive and accessibility behavior

- **Desktop:** persistent search field + one contextual panel adjacent to the map. Autocomplete temporarily opens over or in front of the committed panel.
- **Mobile:** search field remains accessible over the map; autocomplete is a transient suggestion surface, dismissing on selection; the contextual panel is a collapsible **keyboard-operable bottom sheet** (not just a long sidebar stacked above the map).
- Selecting a place should collapse autocomplete and leave the map visible while the user reads place details. Map inspection opens the same panel content, potentially expanding the bottom sheet enough to show the details without completely hiding the map.
- Autocomplete item types must have spoken names/roles such as **"Search for apothe"**, **"Category Apotheke, find nearby"**, and **"Place Markt-Apotheke Linden"**; icons alone are insufficient.
- Maintain a predictable `Escape` hierarchy: close autocomplete draft first, then dismiss foreground inspection, then cancel an uncommitted nearby edit when applicable. The clear-input action is a different operation.
- Preserve keyboard listbox navigation, active-descendant/focus, Return to results, and sensible focus restoration when the sheet/details close.
- Loading and errors should be announced accessibly without stealing focus on every debounced result.
- Map/tile failures must not prevent reading text results. OSM attribution remains visible.

## 8. Acceptance-oriented transition and browser tests

| ID | Scenario | Expected outcome |
| --- | --- | --- |
| T01 | Type `apothe`, wait for debounce | One live `/api/search` fetch for latest query; dropdown shows literal choice, categories, and limited place suggestions; contextual panel is not live-replaced. |
| T02 | Choose literal query with matching loaded snapshot | Dropdown closes; field shows query; panel uses cached **full** list, without duplicate request. |
| T03 | Choose literal query before debounce or while request pending | Panel enters loading; exactly one current query request runs/is adopted; result appears despite dropdown closure. |
| T04 | Choose `Apotheke` category while text-search response is pending | Nearby editor appears with selected category, no nearby HTTP yet; late autocomplete result cannot overwrite it. |
| T05 | Choose autocomplete place | Field becomes place label; details/map reflect exact feature ID; no new forward or reverse HTTP request. |
| T06 | User edits selected-place text then presses Escape | Original place label/details restored; no unintended query committed. |
| T07 | User enters text then clicks blank map before selection | Uncommitted autocomplete is dismissed; reverse inspection takes panel precedence; committed discovery survives. |
| T08 | Forward results active; map background clicked | Forward query/results/markers unchanged; reverse details displayed in contextual panel, with Back. |
| T09 | Nearby active; new text typed, no suggestion chosen | Nearby results remain committed underneath the dropdown; closing draft restores nearby field/panel unchanged. |
| T10 | Nearby category chosen; radius changed | Draft only; no nearby query until Find nearby. |
| T11 | Nearby submitted around map center, then map panned | Applied coordinate, results and radius remain fixed; no automatic requery. |
| T12 | Search this area clicked after manual pan | One new nearby request captures current center and retains applied category/radius. |
| T13 | Nearby markers/list result selected | Known feature details appear, list/marker highlight synchronizes; no map-background reverse request. |
| T14 | Click map A, then map B, reverse responses arrive B then A | Only B appears as foreground inspection. |
| T15 | Locate me succeeds while another discovery active | Device marker/accuracy and reverse update; active discovery unchanged; stale camera intents rejected. |
| T16 | Geolocation denied/unavailable/times out | Proper status shown; map-center fallback explicit and usable; no automatic nearby search. |
| T17 | New committed request fails or returns empty | Show current requested criteria and error/empty feedback, never obsolete results under a new label. |
| T18 | Press Enter with plain valid text versus highlighted suggestion | Plain Enter commits literal query; highlighted Enter selects typed suggestion; no accidental duplicate action. |
| T19 | Clear input while a committed place/nearby/query is active | Explicit neutral state; no automatic restoration of a previous nearby query; viewport/device fix stay. |
| T20 | Select directly from autocomplete then choose Back | Returns to the real prior committed panel; if offered, cached results for autocomplete query are labeled as a separate action. |
| T21 | Search text has <3 or >200 characters | No invalid forward request; user receives guidance/validation feedback. |
| T22 | Category catalog fails | Show retry; All categories nearby remains available when category is optional. |
| T23 | Map tiles fail or app used via keyboard/mobile | Contextual panel/search remain usable, with correct focus and attribution. |
| T24 | Slow autocomplete response arrives after category/place selection | No re-opening of dropdown, overwriting text, or replacing committed panel. |

## 9. Lab 2 constraints retained

These are not changed by the new UI design:

- `/api/search`: `q` free text of 1–200 characters server-side (frontend live search begins at ≥3), `limit` 1–20, default 10. Frontend debounce ≥300 ms.
- `/api/reverse`: coordinate inspection, radius ≤1000 m; UI default 250 m.
- `/api/nearby`: POIs within radius ≤2000 m, optional backend-provided category, distance-ordered results; UI offers 250/500/1000/2000 m.
- `/api/categories`: source of possible nearby POI categories.
- Consistent GeoJSON response contract; `[lon, lat]`, metres, feature identity/labels/type, OSM attribution.
- Explicit **Locate me**; granted/denied/unavailable/timeout handling, accuracy circle, device reverse lookup, secure context.
- Nearby works without device permission using explicit map-center origin. Browser uses own backend API, not another geocoder or direct database access.
- Coordinate privacy, input validation, parameterized SQL, and required Lab 2 test evidence remain mandatory. See the lab instructions for endpoint acceptance and deliverables.

## 10. Decisions to validate in an F5 prototype

The core behavior above is the proposed baseline. The following are presentation/implementation details requiring a real MudBlazor/mobile prototype:

1. **Autocomplete mechanics:** verify custom suggestion types, text coercion, highlighted Enter, Escape, and request handoff behavior; replace the combobox adapter if necessary.
2. **Panel while typing:** choose precise overlay/dimming treatment so old *committed* results do not look like live matches for the new draft.
3. **Search this area placement:** choose an appropriate manual movement threshold and placement, with no noisy prompts from tiny pans or programmatic camera moves.
4. **Mobile sheet:** test map visibility, virtual-keyboard behavior, focus management, map clicks, and returning to cached results.
5. **Selected place display:** define the exact place-detail fields supported by the current GeoJSON response; do not imply external geocoded information that isn't present.
6. **Optional expression syntax:** future `cat:Apotheke` or `radius:500m` could parse into the *same typed selection/actions*. Do not implement a general text/category filter combination that `/api/search` does not support.

Changes to these presentation details should not violate the typed-selection model, one-live-search ownership, immutable nearby origins, non-destructive inspection, or async request-safety invariants.
