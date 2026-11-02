# Engineering review checklist

Use selectively for meaningful implementation or design changes. Trace executed behavior, neighboring components and shared infrastructure—not only the immediately affected lines.

- **System fit:** Is this capability already implemented elsewhere? Are we duplicating a parser, validator, state model, helper, query, transaction primitive or cross-cutting service? Would consolidation make the codebase more coherent?
- **Design rationale:** Is an existing convention justified by actual constraints? Did we inherit questionable patterns without evaluating a cleaner alternative? Is the solution maintainable for future changes?
- **Ownership and dependencies:** Clear service responsibilities, purpose-built DI boundaries, no hidden singletons, circular dependencies, leaky adapter details, misplaced state or ceremonial pass-through layers.
- **Behavior:** Established contract, valid/invalid input, normalization, compatibility, deterministic output, duplicates, ordering and precedence.
- **State:** Logical identity, versioning, lifecycle transitions, ownership consistency, invariants after every exit path, historical/provenance guarantees.
- **Concurrency:** Locking and consistency scope, stale updates, retry/idempotency, cancellation, races, transaction finalization, no partial or orphaned state.
- **Integration:** Published contracts, event ordering/deduplication, auth/permissions, cross-module consistency, negative-path behavior and recovery from drift.
- **Performance:** Appropriate in-database evaluation, narrow projections, joins and indexes, no excessive materialization, N+1 operations or redundant computation.
- **Quality:** Cohesive names and packages, removal of superseded paths, precise error results, tests and steady-state documentation.
- **Validation:** Actual compiler/test/analysis outcomes, relevant integration tests, and honest disclosure of any environment or verification limitations.

For useful refactoring opportunities, assess risk and scope: make small improvements directly; undertake larger low-risk consolidations when their contracts remain stable; recommend disruptive redesigns and obtain maintainer agreement before executing them.
