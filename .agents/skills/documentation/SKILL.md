---
name: documentation
description: Write, review, and maintain all software project documentation, including READMEs, user and developer guides, architecture and reference docs, design decisions, and task plans. Apply audience-oriented, focused, steady-state writing and consistent Markdown formatting.
---

# Documentation

Use this skill for **all documentation work** in a software project: README files, user guides, developer/setup instructions, architecture and design documents, reference material, decision records, task/backlog documents, and documentation that accompanies implementation changes. Apply the same principles to substantial documentation comments when appropriate.

The goal is to help the intended reader understand or accomplish something concrete, using accurate, focused information. For system documentation, explain the current architecture and behavior rather than inventorying implementation details or narrating how the code evolved. For proposals and plans, identify their status and separate intended contracts from implemented behavior.

## Audience, purpose, and ownership

Begin with the reader and the question the document should answer. A user guide should help someone complete a task; a contributor guide should enable a reproducible workflow; an architecture document should explain the system model; a reference should support precise lookup; a design proposal should make a decision reviewable; a backlog should identify actionable work and acceptance criteria. Match the depth, vocabulary, prerequisites, examples, and structure to that purpose.

Give each important concept one **authoritative home**. Use a short overview and a link when another document needs context, rather than repeating its contracts, examples, or rationale. Prefer extending or correcting the existing relevant section over adding a parallel document or a detached appendix. Do not split documentation into tiny files simply to mirror source-code types, and do not grow one catch-all file beyond what its readers can navigate. Organize by meaningful topic, use case, or subsystem boundary.

Favor substantive explanations over boilerplate, generic claims, exhaustive inventories, and redundant recaps. State responsibilities and guarantees precisely, explain non-obvious relationships and tradeoffs, and omit details that do not help the intended reader. Distinguish implemented behavior, agreed design, open proposals, and planned work; do not present any of them as the others.

## Core principles

### Describe the steady-state system

Write documentation in terms of how the system works now.

Avoid changelog-style phrasing such as:

- "The subsystem now does..."
- "Previously..."
- "This was changed to..."
- "The new implementation..."

Use historical context only when the history itself is architecturally relevant, such as explaining a compatibility constraint, migration boundary, or deliberately retained legacy behavior.

Documentation should remain natural and accurate after the implementation has ceased to feel "new."

### Optimize for architectural understanding

For architecture-oriented documentation, prioritize the information a new contributor needs to build an accurate mental model:

- major components and their responsibilities
- core concepts and abstractions
- subsystem boundaries
- lifecycle and data flow
- ownership and responsibility
- important API and behavioral contracts
- ordering and precedence rules
- extension points
- invariants and constraints
- failure behavior where architecturally relevant
- non-obvious interactions and cross-dependencies
- material design tradeoffs

Prefer explaining how these pieces relate to each other over listing classes, methods, files, or signatures.

A reader should understand the system well enough to navigate the implementation intelligently after reading the architecture docs.

### Keep implementation detail at the right abstraction level

Include implementation details only when they materially explain the architecture.

Good reasons to include a detail include:

- it establishes an invariant
- it defines an extension boundary
- it explains an important lifecycle or ordering rule
- it is required because of AOT, source generation, serialization, concurrency, or similar architectural constraints
- it explains ownership or failure semantics
- it reveals a non-obvious dependency between subsystems
- it captures a tradeoff that affects future design work

Do not document incidental helpers, arbitrary class names, private mechanics, or method signatures simply because they exist.

Implementation details belong in architecture documentation only when omitting them would leave the reader with an incomplete or misleading system model.

### Prefer coherent subsystem narratives over inventories

Documentation should read as an explanation, not as a dump of facts.

Prefer:

1. what a subsystem is responsible for
2. which concepts it introduces
3. how data or control enters it
4. how its important internal stages interact
5. what it produces or guarantees
6. how it interacts with neighboring subsystems
7. what extension or failure boundaries matter

Avoid disconnected sections that each describe one arbitrary type or helper without explaining how they fit into the larger system.

Use bullet lists where they improve clarity, but do not let the document become a collection of unrelated bullet points.

### Preserve good existing documentation

When updating an established project, first inspect the existing documentation and identify sections that are already accurate and well written.

Preserve their:

- structure
- tone
- terminology
- level of detail
- degree of abstraction
- formatting conventions
- cross-linking style

Prefer targeted edits over broad rewrites.

Do not rewrite unrelated documentation merely because another formulation is possible. Existing documentation that already communicates the architecture well should be treated as a style and abstraction reference for new material.

### Keep documentation internally coherent

When a change affects a subsystem contract, inspect related documentation for stale descriptions and cross-dependencies.

Check for:

- obsolete architecture descriptions
- changed lifecycle or data flow
- changed ownership or responsibility
- altered precedence or ordering
- stale terminology
- renamed concepts
- changed extension points
- changed failure behavior
- new subsystem interactions
- documentation that still describes an implementation detail that no longer matters

Update every document that materially depends on the changed contract, but leave unrelated sections alone.

Do not fix one document by adding caveats that contradict another document. Prefer one coherent description of the current system across the documentation set.

## Documentation types

Different documents serve different purposes. Keep their abstraction levels distinct.

### Architecture documentation

Architecture documentation should explain the system model in **descriptive, responsibility-focused prose**. State what each component does, what its interfaces guarantee, and which conditions must hold. Express mandatory behavior as an invariant or explicit obligation of the owning component, rather than instructions to an implementer. For example, prefer "Refresh-token rotation is atomic; concurrent reuse invalidates the token family" over "Rotate refresh tokens atomically and reject reuse."

Imperatives are appropriate for procedural guides, implementation checklists, ordered workflows and actionable backlog tasks, where the reader is expected to perform the steps. They are usually out of place in conceptual or architectural explanations. A security-critical prohibition can still be stated directly as a constraint, for example "Only IAM issues primary identity tokens."

Focus on:

- components
- concepts
- responsibilities
- contracts
- interactions
- data/control flow
- lifecycle
- invariants
- extension boundaries
- relevant tradeoffs

Do not turn architecture docs into API reference material.

### Reference documentation

Reference documentation may be more exhaustive and structured around available features, configuration keys, modules, attributes, commands, or APIs.

It should still explain non-obvious semantics and relationships, but completeness is more important here than narrative flow.

Avoid duplicating architectural explanations when a cross-link is sufficient.

### User and developer guides

Organize instructions around tasks and outcomes, with prerequisites, exact commands or inputs, expected results, and relevant failure or recovery guidance. Describe what readers need to do, without mixing in unrelated architectural explanations. Keep commands and examples verified against the supported project state.

### README files

A project README should primarily orient users and contributors.

Typical concerns include:

- what the project is
- what problem it solves
- major capabilities
- basic setup/build/run instructions
- common entry points
- links to deeper documentation

Keep deep architectural explanation in focused documentation unless a short architectural summary is necessary to orient the reader.

### Plans and task backlogs

Describe work in terms of coherent deliverables, dependencies, decisions that must be resolved, and observable acceptance criteria. Keep plans concise and distinguish pending work from the documented current system. Do not duplicate the underlying architecture or domain contract in every task.

### Design or decision documents

When documenting a proposed or deliberately chosen design, clearly distinguish current guarantees from proposed behavior.

Capture:

- the problem or constraint
- the chosen model
- important alternatives when they materially explain the choice
- invariants and consequences
- unresolved questions, if any

Do not let temporary implementation sequencing become part of the permanent architecture description unless it represents a lasting constraint.

## Workflow

### 1. Establish the documentation context

Before editing:

- inspect the existing documentation structure
- identify the documents authoritative for the affected subsystem
- read enough adjacent material to understand the established abstraction level and terminology
- inspect the implementation where needed to verify current contracts and interactions
- identify whether the task is architecture documentation, reference documentation, README work, or a mixture

For implementation-driven documentation work, understand the change set before editing docs.

### 2. Identify what is actually stale or missing

Determine which documented contracts changed.

Look for changes in:

- component responsibilities
- subsystem boundaries
- public or extension contracts
- data flow
- execution lifecycle
- ordering or precedence
- configuration or resolution behavior
- ownership
- error handling
- cross-subsystem dependencies
- terminology

Also identify genuinely undocumented architecture introduced by the change.

Do not assume every changed class requires documentation.

### 3. Update the smallest coherent documentation surface

Make the minimum set of edits needed to restore a correct and coherent system description.

When adding a new concept, place it where readers would naturally expect to learn about it.

When updating an existing concept, modify the existing narrative rather than adding a detached "new behavior" section.

Prefer cross-links over duplicating detailed explanations across documents.

### 4. Perform a coherence cleanup pass

After correctness is restored, reread the touched documents as documentation rather than as a diff.

Remove or improve:

- change-over-time wording
- duplicated explanations
- stale terminology
- arbitrary implementation trivia
- isolated facts that do not fit the surrounding abstraction level
- awkward sections created by repeatedly appending incremental changes
- unnecessary method-signature or class inventories
- misleading examples

Keep this pass conservative. The goal is coherence, not stylistic churn.

### 5. Validate against the implementation

Before finishing, verify that the documentation accurately reflects the current implementation.

Pay particular attention to:

- lifecycle ordering
- precedence rules
- default behavior
- failure semantics
- extension mechanisms
- configuration paths
- names and terminology
- generated behavior
- interactions between independently implemented subsystems

If the implementation and existing documentation disagree, determine the intended current contract rather than silently preserving stale prose.

### 6. Validate the documentation set

Check:

- local links and references
- headings and table-of-contents structure
- terminology consistency
- duplication between documents
- README links to deeper material
- whether related documents describe the same contract consistently
- whether examples still match the documented model

When practical, perform a final diff review specifically for unrelated rewrites or accidental verbosity growth.

## Markdown formatting

- Use **UTF-8 and LF** line endings, with a final newline. Do not use trailing-space hard line breaks; prefer paragraph breaks or explicit structure when separation matters.
- Keep **one logical paragraph per source line**. Do not manually wrap prose to a fixed column width; long Markdown lines are intentional. Separate paragraphs with blank lines, and keep headings, list items, tables, and code blocks structurally distinct.
- Use `-` for unordered lists and **four spaces per nesting level** for nested list items and their continuation blocks. Top-level list markers start at column zero; do not use a two-space nesting convention. Use numbered lists when order matters, and avoid lists when coherent prose is clearer.
- Use **fenced code blocks** with backticks and a meaningful language identifier (`java`, `typescript`, `bash`, `json`, `mermaid`, `text`, etc.). Do not use indentation-only code blocks or leave fences untyped. Choose `text` for plain-text output, directory trees, and pseudocode without an applicable language.
- Use **tables and Mermaid diagrams only as supporting material** when they clarify genuine comparisons, relationships, structure, or data flow. The surrounding prose must explain the model and its important implications independently; do not make a reader infer the primary information from a figure or table alone. Avoid duplicating every cell or arrow in prose, and avoid decorative or gratuitous visuals.
- Preserve correct Markdown semantics when reformatting: do not reflow fenced blocks, diagrams, tables, explicit line breaks, generated regions, or other syntax whose whitespace is meaningful. Validate links, fence languages, and rendered structure after edits.

## Writing guidance

Use examples and code snippets to support an explanation, not to substitute for one.

Prefer a concise example when it demonstrates:

- data flow
- configuration shape
- lifecycle
- an important contract
- an otherwise ambiguous interaction

Avoid exhaustive scenario catalogs in architecture documentation.

Use precise terminology consistently. If the implementation has a meaningful domain term, prefer that term over repeatedly inventing synonyms.

Explain why a constraint matters when the reason is architecturally significant, but avoid speculative rationale that is not supported by the system or its documented design.

Keep prose direct. Prefer concrete statements about responsibilities and guarantees over promotional language or vague descriptions. Avoid rhetorical contrasts ("X does Y, not Z", "not merely", "rather than", etc.) when the negative half adds no meaningful constraint. Describe the positive contract directly. Retain explicit exclusions when they prevent a real misinterpretation, such as distinguishing an event-delivery registry from a business journal, or distinguishing authentication from authorization. Do not repeat the same contrast across adjacent sections.

## Common failure modes

Avoid these patterns:

### Instructional architecture prose

Bad:

> Use framework-provided password encoders. Avoid custom hashing.

Better:

> Credential verification uses Spring Security's `PasswordEncoder`; the IAM adapter selects and upgrades password-hash parameters.

### Empty contrast prose

Bad:

> The publication registry tracks delivery, not business history. The registry is not an event store.

Better:

> The registry records pending deliveries. Finance's ChangeRecord journal owns business-visible financial history.

The original contrast is worth keeping if it appears once where readers could genuinely confuse the responsibilities.

### Changelog documentation

Bad:

> The runtime now invokes validation before execution.

Better:

> The runtime invokes validation before execution.

### Implementation inventory

Bad:

> `FooManager` calls `FooResolver.Resolve()`, then `FooProcessor.Process()`, then `FooWriter.Write()`.

Better:

> The subsystem resolves the effective configuration, processes it into the runtime model, and publishes the resulting state.

Use concrete type names only when the type itself is an important architectural abstraction.

### Detached implementation trivia

Do not add isolated details such as specific helper classes, private collections, temporary workarounds, or internal method names unless they establish a meaningful contract or invariant.

### Architecture by method signature

Avoid using a list of method signatures as the primary explanation of a subsystem. Explain the concepts and lifecycle first; link to API/reference documentation for exhaustive signatures.

### Rewriting good documentation unnecessarily

Do not perform broad stylistic rewrites while fixing one stale section. This increases review noise and risks degrading established documentation.

### Documenting temporary implementation state as architecture

If a design is intentionally transitional, document the stable contract that users and maintainers should rely on. Mention the transitional constraint only when it materially affects current behavior or future work.

## Completion criteria

Documentation work is complete when:

- the affected system behavior is accurately described
- important new architecture or interactions are documented
- stale contracts and terminology are removed
- related documents agree with each other
- the existing documentation's useful structure and style are preserved
- the text describes the steady-state system rather than the implementation history
- architecture docs remain focused on concepts, contracts, lifecycle, boundaries, and interactions
- implementation trivia has not displaced architectural explanation
- examples and links are valid
- a new contributor could use the documentation to form a coherent mental model of the affected area
