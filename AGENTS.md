# EliteMUD-CS Agent Guide

This file guides agentic coding assistants working in this repository.

## Repository Overview
- Language/runtime: C# on .NET 10, targeting Linux.
- Solution: `EliteMUD.sln` at repo root.
- Projects:
  - `src/EliteMud.Server`: Telnet server, bootstrap, session loop.
  - `src/EliteMud.Game`: Core domain models (rooms, mobs, zones).
  - `src/EliteMud.Scripting`: Lua integration via MoonSharp.
  - `src/EliteMud.Data`: SQLite contracts for runtime persistence.
  - `src/EliteMud.Legacy`: Legacy loaders (planned).
  - `tests/EliteMud.Tests`: xUnit tests (currently minimal).
- Content data lives in versioned JSON under `content/`.

## Build / Test / Run
### Build the solution
- `dotnet build EliteMUD.sln`

### Run the server
- `dotnet run --project src/EliteMud.Server`
- Optional port override: `dotnet run --project src/EliteMud.Server -- 7600`

### Run all tests
- `dotnet test EliteMUD.sln`

### Run a single test
- By test name: `dotnet test tests/EliteMud.Tests --filter "FullyQualifiedName~Namespace.ClassName.TestName"`
- By class: `dotnet test tests/EliteMud.Tests --filter "FullyQualifiedName~Namespace.ClassName"`
- By trait (if added): `dotnet test tests/EliteMud.Tests --filter "Category=Unit"`

### Lint / Format
- No dedicated linter configured yet.
- If formatting is needed later, prefer `dotnet format` once adopted.

## Code Style Guidelines
### General
- Keep changes small and cohesive. Prefer refactors that improve clarity without altering behavior.
- Avoid unrelated changes during feature work.

### Naming
- Types: `PascalCase` (e.g., `WorldState`).
- Methods: `PascalCase`.
- Locals/parameters: `camelCase`.
- Async methods: suffix with `Async`.
- Prefer explicit names over abbreviations.

### Files and Layout
- One public type per file where practical.
- Keep `Program.cs` minimal (entry point only).
- Group server concerns in `src/EliteMud.Server` and domain models in `src/EliteMud.Game`.

### Imports / Usings
- Use file-scoped namespaces (`namespace EliteMud.Server;`).
- Remove unused `using` directives.
- Keep system usings first, then project usings.

### Formatting
- 4-space indentation.
- Curly braces on new lines (C# default).
- Keep lines reasonably short; wrap long argument lists.

### Types and Nullability
- Prefer records for immutable data models.
- Use nullable reference types intentionally (`string?`), avoid null where possible.
- Validate inputs at boundaries (file I/O, network data).

### Error Handling
- Fail fast for configuration errors; log a clear message to console.
- For content parsing, prefer graceful fallback with a warning.
- Avoid swallowing exceptions silently; log with context.

### Async / Concurrency
- Use `async`/`await` for I/O.
- Prefer `CancellationToken` parameters in async loops.
- Avoid blocking calls in the Telnet session loop.

### Networking / Telnet
- Keep Telnet parsing minimal and robust.
- Avoid per-byte allocations in hot paths.
- Treat input as untrusted; sanitize and trim.

### Content Loading
- Content files are JSON under `content/`.
- Keep content schemas forward-compatible (versioned).
- Prefer adapters instead of in-place format changes.

### Scripting
- Lua via MoonSharp in `EliteMud.Scripting`.
- Restrict scripts to safe APIs via exposed bindings.
- Scripts may be filtered by room ID when present.

### Persistence
- Use SQLite for runtime/player data only.
- Keep world content in versioned files, not the DB.

## Project Conventions
- Commands like `zreset` reset zone mobs; update content if behavior changes.
- Keep bootstrap content as fallback if `content/` is missing.
- When adding new content types, update `docs/content-schema.md`.

## Documentation
- Keep roadmap in `docs/roadmap.md`.
- Keep content schema in `docs/content-schema.md`.

## Cursor/Copilot Rules
- No `.cursorrules`, `.cursor/rules/`, or `copilot-instructions.md` found in this repo.
