# EliteMUD-CS

EliteMUD-CS is an in-progress C# rewrite of the original EliteMUD codebase. The
project aims to preserve legacy gameplay while moving the server to .NET 10,
versioned JSON world content, SQLite persistence, and sandboxed Lua scripting.

The server supports multiple Telnet sessions, account and character creation,
character persistence, world exploration, inventory and equipment, combat,
skills and spells, zone resets, and several NPC behaviors. Legacy parity is
still under development; see the [roadmap](docs/roadmap.md) for current status
and known gaps.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Telnet-compatible MUD client or command-line Telnet client
- Linux, macOS, or Windows for development; Linux is the deployment target

## Quick Start

Clone the repository, then run these commands from its root:

```bash
dotnet restore EliteMUD.sln
dotnet run --project src/EliteMud.Server
```

The server listens on all interfaces on port `7500` by default. Connect with a
MUD client or Telnet:

```bash
telnet localhost 7500
```

If Telnet is unavailable, a basic connection can also be made with Netcat:

```bash
nc localhost 7500
```

Follow the prompts to create an account and character. Stop the server with
`Ctrl+C`.

To use a different port, pass it as the first argument:

```bash
dotnet run --project src/EliteMud.Server -- 7600
```

## Development

Build the solution:

```bash
dotnet build EliteMUD.sln
```

Run all tests:

```bash
dotnet test EliteMUD.sln
```

Run a specific test class or test by name:

```bash
dotnet test tests/EliteMud.Tests --filter "FullyQualifiedName~Namespace.ClassName"
dotnet test tests/EliteMud.Tests --filter "FullyQualifiedName~Namespace.ClassName.TestName"
```

## Content and Persistence

World definitions are stored as versioned JSON. The server searches upward
from its working directory for `content/`, so running it from the repository
root is recommended.

- `content/` contains the monolithic room, mob, object, zone, script, skill,
  and spell definitions.
- `zones/` contains grouped legacy world files, one file per zone. When this
  directory exists beside `content/`, the server loads it before the
  monolithic world files.
- `elitemud.db` is the local SQLite database for accounts, characters, and
  runtime state. It is created beside `content/` and migrations are applied
  automatically at startup.

The SQLite database is intentionally ignored by Git. World content belongs in
versioned files rather than the runtime database. See the
[content schema](docs/content-schema.md) before changing content formats.

## Legacy Content Importer

`EliteMud.Cli` converts legacy EliteMUD world data to JSON and can group an
imported world into per-zone files.

Show the available commands:

```bash
dotnet run --project src/EliteMud.Cli -- --help
```

Import legacy world files:

```bash
dotnet run --project src/EliteMud.Cli -- import ../EliteMUD ./output
```

Group imported content by zone:

```bash
dotnet run --project src/EliteMud.Cli -- group ./output ./zones
```

The importer accepts `--no-rooms`, `--no-zones`, `--no-mobs`, and
`--no-objects` when only part of a legacy world should be converted.

## Solution Layout

| Path | Responsibility |
| --- | --- |
| `src/EliteMud.Server` | Telnet host, session loop, adapters, startup, and content loading |
| `src/EliteMud.Application` | Commands, authentication, combat orchestration, AI, skills, spells, and world state |
| `src/EliteMud.Game` | Core domain models and gameplay calculations |
| `src/EliteMud.Data` | EF Core SQLite context, repositories, entities, and migrations |
| `src/EliteMud.Scripting` | MoonSharp Lua hooks and formula evaluation |
| `src/EliteMud.Legacy` | Legacy world parsers and import services |
| `src/EliteMud.Cli` | Command-line legacy content importer |
| `tests/EliteMud.Tests` | xUnit test suite and fixtures |
| `content` | Versioned world and gameplay definitions |
| `zones` | Grouped legacy zone content |

## Documentation

- [Development roadmap and feature status](docs/roadmap.md)
- [World content schema](docs/content-schema.md)
- [Legacy character creation reference](docs/LEGACY_CHARACTER_CREATION.md)
- [Skills and spells proof of concept](docs/skills-poc-documentation.md)

## Project Status

EliteMUD-CS is under active development and is not yet a production-ready
replacement for the legacy server. Compatibility with original EliteMUD
behavior is preferred when implementing or changing gameplay systems.
