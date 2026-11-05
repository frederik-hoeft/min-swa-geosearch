# Search workspace state and request lifetimes

**Scope:** F2–F4 orchestration cleanup ahead of F5. The interaction contract remains [F5 interaction model](f5-interaction-contract.md); this refactor does not introduce nearby-search behavior.

## Ownership

- **`SearchWorkspace` (Razor + code-behind):** reads immutable state, renders input/suggestions/details, translates keyboard and UI events, forwards map callbacks, and dispatches external component-reference calls through the Blazor renderer. It does not own HTTP clients, timers, or request cancellation sources.
- **`SearchWorkspaceWorkflow`:** page-owned coordinator for `ExploreSearchState` transitions and downstream events (`OnCommitted`, map-state projection, UI refresh). It decides whether an action edits, commits, inspects, navigates Back, or clears. It owns and disposes the short-lived sessions; they are intentionally *not* application-wide DI singletons.
- **`SearchPreviewSession`:** owns draft debounce, preview result/hint, and autocomplete suggestions. Superseded drafts cannot publish late previews; a confirmed literal query can still adopt the successful snapshot or in-flight request.
- **`CommittedSearchSession`:** guards forward search/refresh responses by generation, invalidated on any action that supersedes the committed query. It uses the same `SearchQuerySession` cache as autocomplete to avoid duplicate HTTP requests.
- **`ReverseLookupSession`:** owns one reverse lookup at a time and its cancellation/generation checks. Late responses (including a transport that ignores cancellation) are ignored after a newer point, Back, place selection, or teardown.
- **`BackgroundMapClickCoordinator`:** retains the map gesture's single-click/double-click confirmation window; its confirmed callback is dispatched onto the Blazor renderer before updating workflow state.
- **`ExploreSearchState`:** immutable state machine for draft/committed panel transitions and Back targets. Neither component nor request sessions invent alternate copies of the committed state.

## Invariants and F5 extension

1. Draft edits cannot change committed results or map markers. The preview session never owns the committed panel.
2. Literal-query commit may adopt a successful preview or a shared in-flight search task. Failed requests are evicted by `SearchQuerySession`, allowing retry.
3. Reverse inspection leaves the underlying forward result set available for Back; a pending forward completion can still populate that return target.
4. Map marker selection uses known feature data and does not refetch. A direct-autocomplete place keeps its previous result markers hidden until Back.
5. Clicks that dismiss autocomplete do not also reverse-inspect; a double-click cancels pending inspection while preserving MapLibre zoom.
6. An asynchronous completion may update the view only while its session generation remains current. Disposing the workspace invalidates outstanding work.

For F5, extend the typed suggestion/panel states and add a **separate nearby request/session** with its own origin snapshot and cancellation semantics. Keep nearby requests out of `SearchWorkspace`'s event handlers rather than adding more request fields to the component. If the nearby API shares the same request shape with forward search, reuse generic request helpers without conflating their state generations.

## Validation

The existing renderer tests cover map selection, direct-autocomplete Back behavior, reverse-inspection races, and map clicks with open suggestions. Additional `SearchPreviewSessionTests` and `CommittedSearchSessionTests` check independently superseded previews, snapshot reuse, and out-of-order committed responses. Browser/WebGL and actual on-screen keyboard behavior still require manual acceptance.
