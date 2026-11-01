---
name: engineering-quality
description: Apply proactive, architecture-aware software engineering when implementing, refactoring, debugging, or reviewing code. Use to preserve coherent ownership and data flow, find and eliminate redundant infrastructure, challenge questionable patterns, improve maintainability, and validate behavior and invariants across the whole system.
---

# Engineering quality

Optimize for a **coherent, correct, maintainable codebase**, not merely a working local change. Reason about the system that will remain after the task: its responsibilities, invariants, data flow, dependency structure, and ease of future modification. A little more work now is justified when it removes duplication or avoids permanent architectural debt.

Use sound judgment rather than enforcing a fashionable architecture mechanically. Follow genuine project requirements and established contracts, but **question existing patterns when their rationale is weak or a better design is available**. Repetition is evidence of a convention, not proof that the convention is good.

## Understand the system before adding to it

1. Read the relevant architecture, domain and API documentation; examine neighboring implementations and tests. Trace the complete operation through entry points, services, data access, side effects and consumers.
2. Search beyond the immediately affected files for existing abstractions, validators, parsers, utilities, state machines, models, and infrastructure serving the same purpose. Prefer extending or consolidating a sound existing capability over building a parallel implementation.
3. Identify state ownership, invariants, transactional and concurrency boundaries, lifecycle assumptions, failure modes, and externally observable contracts. Consider whether the proposed change belongs in the component where it first appears, or in a shared owner elsewhere.
4. Examine the *reason* for existing design choices. Preserve conventions that serve a real purpose; improve conventions that introduce avoidable coupling, duplication or incidental complexity. Do not reproduce an awkward pattern merely because similar code already exists.
5. Consider how the change will interact with other operations, current consumers, new use cases, and eventual architectural evolution. Avoid solving one symptom while leaving the underlying structural problem intact.

## Design and implementation preferences

- **Clear separation of concerns:** Keep responsibility, ownership and dependency direction explicit. Expose purposeful contracts between modules; avoid reaching into neighboring components' internal state or data. Favor operation-focused interfaces and cohesive service boundaries over broad all-purpose abstractions.
- **Dependency-injected services:** Prefer dedicated, DI-managed services for meaningful behavior, orchestration, policies or functionality with dependencies. Avoid manually managed singletons, global mutable state, static service locators, and large static utility classes that hide dependencies or accumulate unrelated behavior. Small, pure and stateless helper functions are fine when introducing a service would add no real value.
- **No ceremonial layers:** Do not introduce interfaces-per-class, pass-through facades, chains of wrappers, dependency injection for plain data objects, or needless micro-services/classes. Abstractions should provide testable boundaries, meaningful capabilities, or genuine reuse—not satisfy a diagram.
- **Reuse before reinvention:** Consolidate duplicated code and remove obsolete implementations when safe. Extend a parser, query builder, validation framework, transaction mechanism or other established capability rather than hand-rolling a second version. Reuse is not an excuse to preserve an abstraction that is itself flawed: refactor the common mechanism when that is the cleaner solution.
- **Deliberate data flow:** Keep APIs and data representations suited to their operations. Favor typed inputs, explicit results and narrow projections. Where appropriate, evaluate filtering, aggregation, predicates and atomic conditional writes close to the data instead of materializing full object graphs. Do not make efficiency an excuse to duplicate or contradict business policy inside adapters.
- **Appropriate scope of state:** Make lifetime, mutability, thread-safety and resource ownership evident. Prefer simple, explicit dependencies and operation-local state over implicit shared state or caching.
- **Meaningful names and organization:** Use cohesive packages/modules and accurate domain terminology. Split overloaded classes and crowded namespaces when responsibility boundaries warrant it; avoid gratuitous fragmentation. Favor code that a future maintainer can understand without reconstructing the author’s path.
- **Pragmatic extensibility:** Design for realistic change and known integration boundaries, without inventing speculative frameworks or indirection. Prefer removing an accidental constraint to adding another workaround.

## Proactive improvement and task scope

Do not interpret the request's initial file list or immediate symptom as a prohibition on improving nearby code. Assess opportunities by **coherence, risk and contract impact**, not just by line count:

- **Minor improvements:** Make small, clearly beneficial refactorings directly while working: eliminate a duplicate, simplify a misleading helper, correct naming, reuse a common validator, or remove unnecessary indirection. Keep these changes cohesive with the task and verify affected behavior.
- **Larger, low-risk refactorings:** It is acceptable to expand the task to consolidate shared behavior, fix an unsuitable boundary, or reorganize closely related code when existing contracts and behavior can be preserved with high confidence. Include the necessary callers and tests; do not leave two competing paths.
- **Very large or high-risk refactorings:** Surface the architectural problem, explain why the better design is preferable, what it affects, and an incremental migration path. **Seek maintainer approval before executing** a disruptive redesign, especially when it changes public contracts, persistence semantics, or ownership.

This is not a mandate to refactor everything encountered. Weigh benefits against regression risk, integration cost and comprehensibility. But equally, do not reject an important improvement solely because a quick local workaround would be easier or involves fewer files. If the best long-term solution is out of scope, say so explicitly rather than silently installing another redundant mechanism.

## Correctness is architectural, not just local

- Validate invariants across **all** relevant entry points and mutation paths, including updates, deletion, retries, reordering, partial failure, and reconciliation. Prefer typed models and centralized validation rather than brittle re-parsing or scattered ad hoc checks.
- Trace concurrent and asynchronous flows: stale inputs, competing writes, ordering, cancellation, idempotency, resource disposal, and transaction finalization. Distinguish local atomicity from eventual consistency across boundaries.
- Use ordinary typed outcomes for expected conditions when practical; reserve exceptions for unexpected failures or violated contracts. Preserve useful diagnostics. A failed lower-level operation must not accidentally permit a higher-level transaction to commit inconsistent state.
- Treat external data, authentication, authorization and serialization as trust boundaries. Fail closed when an ambiguous state would otherwise compromise correctness or security.
- Preserve historical identity, provenance and version semantics whenever the domain depends on them. Optimization must not silently alter the behavior or precision of an established contract.
- Consider performance as part of design: avoid redundant work, excessive allocations, unnecessary data transfers, N+1 queries and repeated parsing. Optimize thoughtfully, backed by evidence where feasible.

## Testing, review and documentation

- Test observable behavior and invariants, not incidental private implementation shapes. Cover negative and adversarial inputs, boundary conditions, concurrency conflicts, rollback/recovery, and compatibility where relevant. Add regression tests when fixing defects.
- Test real integration boundaries when mocks cannot prove their semantics—particularly persistence, transactions, query behavior, serialization and event delivery. Favor deterministic and focused tests.
- Validate with the available build, test, formatter, static-analysis and architectural checks. Distinguish tooling limitations from product defects; never claim checks ran when they did not.
- Review the surrounding call graph and impacted contracts, not only the directly edited code. Actively look for existing redundant paths, stale assumptions, cross-component inconsistencies and missed simplification opportunities. Report meaningful systemic issues even when a narrowly scoped change appears correct.
- Keep documentation oriented around **steady-state responsibilities, data flow, guarantees and rationale**. Explain what a component does and why non-obvious decisions matter. Update related documentation when a change affects its contracts; avoid narrating development history.
- Communicate material tradeoffs, residual risks and architectural alternatives clearly. Separate necessary corrections from optional improvements, and avoid burying a serious design concern under stylistic notes.

Use [the review checklist](references/review-checklist.md) for a deeper pass when the change warrants it. Apply it proportionally to the actual risks rather than as a mechanical checklist for every trivial edit.
