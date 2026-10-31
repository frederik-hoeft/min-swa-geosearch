# Code style

This repository follows the conventions used in [UFWeb](https://github.com/frederik-hoeft/ufw-webui/blob/main/code-style.md). The `.editorconfig` and `.globalconfig` in `src/` are the enforceable baseline. Where a convention and generated/third-party code differ, treat generated/third-party code as external.

We use file-scoped namespaces, Allman braces and 4-space indentation. Specify access modifiers even where implied; avoid `var`, preferring explicitly named locals. Name async task-returning methods with `Async`. Avoid unrelated top-level types in a file, and use one file per meaningful unit. Prefer restricted visibility (`internal`, `private`, `sealed`) until extension is required.

Place using directives outside namespaces and alphabetize them. Prefer `nameof`, language keywords such as `int`, target-typed `new` only where the declaration establishes the type, and strongly typed configuration over magic strings. Use nullable annotations and propagate cancellation tokens on async I/O. Maintain scope-specific SCSS/CSS; page-local styles use Blazor CSS isolation, reusable style rules use component-specific classes and centralized generic MudBlazor customization.

Isolate application contracts from infrastructure DTOs, EF entities, GIS vendor models, and rendering frameworks. Keep one source of truth per concern and avoid speculative abstractions. Unit-test pure behavior and integration-test database/provider behavior against real dependencies. No secrets, credentials, location query strings or private key material in logs. `dotnet format` and `dotnet test` are the final authority on compiler-supported style violations.
