# EliteMUD Legacy Content Importer

A command-line tool to convert legacy EliteMUD world files to modern JSON format.

## Installation

### Build from source
```bash
dotnet build src/EliteMud.Cli/EliteMud.Cli.csproj
```

### Run directly
```bash
dotnet run --project src/EliteMud.Cli -- [arguments]
```

### Create standalone executable
```bash
dotnet publish src/EliteMud.Cli/EliteMud.Cli.csproj -c Release -r linux-x64 --self-contained
```

## Usage

```bash
elitemud-import import <legacy-path> <output-path> [options]
```

### Arguments

- `<legacy-path>` - Path to legacy EliteMUD world directory (containing wld, mob, obj, zon folders)
- `<output-path>` - Path to output JSON content directory

### Options

- `--no-rooms` - Skip importing rooms
- `--no-zones` - Skip importing zones
- `--no-mobs` - Skip importing mobs
- `--no-objects` - Skip importing objects

## Examples

### Import all content
```bash
dotnet run --project src/EliteMud.Cli -- import ../EliteMUD/lib ./content
```

### Import only mobs and objects
```bash
dotnet run --project src/EliteMud.Cli -- import ../EliteMUD/lib ./content --no-rooms --no-zones
```

### Using published executable
```bash
./elitemud-import import /path/to/legacy/world ./output
```

## Output Format

The tool generates JSON files in the following structure:

```
output-path/
├── rooms/
│   └── rooms.json
├── zones/
│   └── zones.json
├── mobs/
│   └── mobs.json
└── objects/
    └── objects.json
```

Each JSON file follows the v1 content schema documented in `docs/content-schema.md`.

## Legacy Format Support

The importer supports the following legacy EliteMUD formats:

- **Rooms**: Standard DikuMUD room format with exits, descriptions, and flags
- **Zones**: Zone definitions with reset commands (M/O/P/G/E/D/R)
- **Mobs**: S (simple), A (auto), and legacy mob formats with attacks, skills, resistances, and programs
- **Objects**: All item types with E/A/B/P extensions (extra descriptions, affects, bitvectors, special procs)

## Notes

- The tool expects legacy files to be in ASCII format
- All numeric IDs are preserved from the legacy format
- Unmapped flags/enums are converted to numeric labels (e.g., `Skill_304`, `Resist_1`)
- Output JSON is formatted with indentation for readability
