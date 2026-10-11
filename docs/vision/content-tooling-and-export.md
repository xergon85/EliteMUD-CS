# Content Tooling and Legacy Export

_Status: idea · Added: Oct 11, 2026_

## The idea

Build tools to manage world content (zones, rooms, mobs, objects) instead of fixing legacy data by hand. Later, make
content created with those tools usable on the **live server**, which still runs the original C code.

The export is **targeted, not a full data dump**: for example, a zone made in a zone builder can be exported as legacy
files and added to the live game.

## Why

The legacy import work (see `docs/reports/legacy-import-audit-2026-10-10.md`) showed how much in the world data is
silently broken or ambiguous:
- old copies of zones
- files the original never loaded
- duplicate ids
- typos that drop mobs and rooms
- keys and portals that nothing creates

Tools that check content as it's made would stop this from happening again, and a builder would let us create new
content for both the rewrite and the live game.

## Shape

One validated content model in the middle, with adapters on either side:

```
legacy files ──> importer ──> content model (JSON) ──> exporter ──> legacy files for live
                                     │
                       validation, reports, builder tools
```

This matches the "prefer adapters instead of in-place format changes" rule in `AGENTS.md`.

## Tools to build on the content model

- **Reference checker and reachability report:** no broken references; warn about unreachable rooms and items nothing
  creates. Planned already (see the audit's suggested order).
- **`validate-content` command:** run in CI and before publishing.
- **Generated reports:** replace hand-written audits and object lists.
- **Zone builder:** OLC in-game (on the roadmap) or a small web tool, working on the validated model so it can't produce
  broken content.

## Per-zone export to the legacy format

What exporting one zone needs:
- **File writers** for `N.wld`, `N.mob`, `N.obj`, `N.zon` and optionally `N.shp`, plus the line to add to each folder's
  `index`. Legacy conventions: Latin-1 encoding, `~`-terminated strings, letter or number bit flags, C struct limits.
- **A reference check:** every room, mob, object and key the zone uses exists inside the zone or on the live server.
- **A number check:** room, mob, object and zone numbers must not clash with the live server. The zone's room range has
  to fit the legacy rule: a zone covers the rooms after the previous zone's top room, up to its own top room.
- **A strict boundary:** content using features the old engine lacks (Lua formulas, the new class and race data, new
  reset types) refuses to export instead of silently losing them.
- **A format test:** export a zone, import it again, and check the data is unchanged. This proves the writers produce
  files the legacy parser reads correctly.

Because exported zones are new content, the importer doesn't need to be byte-for-byte lossless for this.

## Open questions

- **The live baseline:** we need the live server's current world files (or at least its list of used numbers). Our
  source snapshot is older: New Thalos isn't in its index, and clans have been renamed since.
- **Builder:** in-game OLC, a web tool, or both?
- **Delivering exports:** how would an exported zone reach the live server, and who installs it there?
