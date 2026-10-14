# EliteMUD-CS Agent Guide

This file guides agentic coding assistants working in this repository.

## Repository Overview
- Targets Linux; content data lives in versioned JSON under `content/`.

## Run
- `dotnet run --project src/EliteMud.Server`
- Optional port override: `dotnet run --project src/EliteMud.Server -- 7600`
- Admin commands (`zreset`, `goto`, `load`, `setlevel`, `setskill`, `setspell`) only work for accounts listed in
  `elitemud.config.json` at the repo root (gitignored; copy `elitemud.config.example.json`). No file = no admins.

## Code Style Guidelines
### General
- Avoid unrelated changes during feature work.
- Keep `Program.cs` minimal (entry point only).
- Prefer records for immutable data models.

### Error Handling
- Fail fast for configuration errors; log a clear message to console.
- For content parsing, prefer graceful fallback with a warning.

### Async / Concurrency
- Avoid blocking calls in the Telnet session loop.
- Game state changes one thing at a time: commands, tick passes, joins/leaves and save reads hold `GameStateGate`.
  Sending output inside it is fine (sessions queue it); database work is not, and the gate isn't re-entrant.

### Networking / Telnet
- Keep Telnet parsing minimal and robust.
- Avoid per-byte allocations in hot paths.
- Treat input as untrusted; sanitize and trim.

### Content Loading
- Keep content schemas forward-compatible (versioned).
- Prefer adapters instead of in-place format changes.

### Scripting
- Lua via MoonSharp in `EliteMud.Scripting`.
- Restrict scripts to safe APIs via exposed bindings.
- Scripts may be filtered by room ID when present.

### Persistence
- Use SQLite for runtime/player data only.
- Keep world content in versioned files, not the DB.
- **Never drop tables/columns that hold data without a migration** — follow `docs/database-migrations.md` for any
  EF Core schema change.

## Project Conventions
- Commands like `zreset` reset zone mobs; update content if behavior changes.
- Keep bootstrap content as fallback if `content/` is missing.
- When adding new content types, update `docs/content-schema.md`.

## Legacy Source Lookup
- Original C source: `~/Dev/elitemud/src` (frozen; several files are ISO-8859-1, so plain `grep` misses matches).
- Indexed UTF-8 copy: `~/Dev/elitemud-index` (read-only, same line numbers as the original, cscope index prebuilt).
- Prefer cscope over grep (`cd ~/Dev/elitemud-index` first):
  - Definition: `cscope -d -L -1 advance_level`
  - Callers: `cscope -d -L -3 gain_exp`
  - Functions called by: `cscope -d -L -2 group_gain`
  - All uses of a symbol: `cscope -d -L -0 titles`
  - Text search: `cscope -d -L -4 "You receive"`
- Commands defined with `ACMD(do_x)` / `SPECIAL(x)` show the macro name as the enclosing function; use the line number.
- The index never needs rebuilding unless the legacy source changes.

## Documentation
- Keep roadmap in `docs/roadmap.md`.
- Keep content schema in `docs/content-schema.md`.
- EF Core migration procedure: `docs/database-migrations.md`.
