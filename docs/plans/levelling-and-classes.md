# Plan: Levelling, Races & Classes

_Created: Oct 10, 2026 · Status: not started_

**Goal:** Killing things should lead somewhere. Players earn XP using the legacy rules, level up,
get HP/mana/move and stat gains based on their class and race, and see their progress in
`score`/`who` with their legacy titles.

**Legacy sources** (in `~/dev/elitemud/src`; note several files are ISO-8859-1, so use `grep -a`):

| What | Where |
|---|---|
| XP needed, level-up, gain/lose XP | `limits.c`: `exp_needed`, `advance_level`, `improve_abilities`, `gain_exp`, `gain_exp_regardless`, `set_title` |
| Kill / damage / death XP | `fight.c`: `die` (~541), `group_gain` (~559), damage XP (~894), kill XP (~1036) |
| Flee XP loss | `act.offensive.c` (~453) |
| Title + XP tables (20 classes × 40 rows) | `constants.c`: `titles[20][40]` (~987) |
| Race stat caps, starting vitals, allowed classes | `constants.c`: `str_max[]`…`cha_max[]`, `hp_start[]`, `mn_start[]`, `mv_start[]`, `allowed_classes[]` (~174) |
| Race title names | `constants.c`: `race_title_names[]` (~144) |
| Mob XP value | `db.c` (~1346): `GET_EXP(mob) = exp_needed(mob)/10` for generated mobs; zone files store explicit values |
| Level constants | `structs.h` (~741): `LEVEL_WORSHIP 100`, `LEVEL_DEITY 110`, … `LEVEL_IMPL 116` |

---

## How Legacy Works (summary)

- **XP is per level, not cumulative.** On level-up, XP resets to 0.
- **XP needed** comes from the class's title table, one row per 3 levels, interpolated:
  `needed = titles[class][level/3+1].exp + (diff/3) * (level % 3)`, where `diff` is the gap to the next row.
- **Per-gain cap:** a single gain is capped at `needed / (10 + level/15)`. Remort 5+ always gets 1 XP.
- **Mortals stop at level 109** (`LEVEL_DEITY - 1`). 110+ are immortals.
- **Sources of XP:**
  - Damage: `+victim.level * dam / 2`, **not in PvP**. This is added directly, without the cap or level-up check.
  - Solo kill: `victim.exp / 4` (min 1).
  - Group kill: `victim.exp / (2 + members)`, split by `level² + 25` weighting (needs groups, so out of scope here).
  - Death: `-exp_needed / 2`. Flee: lose XP based on the fight.
- **Level-up gains** (`advance_level`):
  - HP: `1d5 + CON / k`, where k = 5 (mages), 4 (priests), 3 (rogues), 2 (fighters)
  - Mana: +10 (mages, priests) or +5 (rogues, fighters); none at level 1
  - Move: +3
  - Practices: `+max(1, WIS/3)`, capped at 100
  - Random attribute gains toward the race maximum, favoring the class's prime stats (`improve_abilities`)
  - Title updated to "the {Race} {class title for level}"
  - Multi-class characters divide gains by 2 or 3
- **Races** cap each attribute (`str_max` etc.) and set starting HP/mana/move.

## What the C# Code Has Today

- `Race` and `CharacterClass` enums. Creation menus with race-based class restrictions (`AllowedClasses`).
- `Experience`, `Level` and `Title` on the character, saved to the DB. No practices field.
- Every character starts with 11 in all stats, 20 HP, 100 mana and 100 move, regardless of race or class.
- Mob kill XP is `mob.Level * 100` (`GameTickService.HandleMobDeath`). This isn't legacy.
  The zone files' `"Experience"` values are **not loaded** into `MobDefinition`.
- PvP kill XP is `victim.Level * 100`. Damage XP is also given in PvP, which legacy doesn't do.
- Death penalty is 10% of current XP (placeholder). Flee XP loss is roughly ported.
- `score` shows a fake "exp needed" (`level * 1000`).
- No level-up at all.
- `skills.json` already has per-class `minLevel` / `maxProficiency`.
- **Enum mismatch:** C# `TwoClassMulti = 20`, `ThreeClassMulti = 21`. Legacy is 20 = NONEWA, 21 = DUAL, 22 = 2MULTI, 23 = 3MULTI.

---

## Tasks

Ordered so each step is testable on its own. ☐ = to do.

### A. Data foundation

- ☐ **A1. Port the class table to content.** Create `content/classes/classes.json` with, per class:
  id, name, abbreviation, HP divisor (5/4/3/2), mana per level, prime stats for attribute gains,
  and the 40 title/XP rows (male title, female title, exp) from `titles[][]`.
  Write a small one-off extractor rather than hand-copying 800 rows.
  _Done when:_ the file loads at startup and every playable class has 40 rows.
- ☐ **A2. Port the race table to content.** Create `content/races/races.json` with:
  stat maxima, starting HP/mana/move, allowed classes, title name, and average age/height/weight.
  _Done when:_ the values match `constants.c` for all 25 races.
- ☐ **A3. Registries.** Add `ClassRegistry` and `RaceRegistry` that load the JSON, following `SkillMetadataRegistry`.
  Warn and fall back gracefully if a file is missing.
  Update `docs/content-schema.md`.
- ☐ **A4. Fix the `CharacterClass` enum** to match legacy numbering (17, 18, 20 = none; 21 Dual, 22 2Multi, 23 3Multi).
  Check the DB stores class names, not numbers, so no migration is needed.
- ☐ **A5. Load mob XP from content.** Add `Experience` to `MobDefinition` and read it in `ContentLoader`.
  _Done when:_ a Midgaard mob reports its file value.

### B. Core XP rules

- ☐ **B1. `ExperienceService.ExpNeeded(level, class)`**, matching `exp_needed` including the `/3` interpolation and the deity cap.
  Unit-test it against hand-computed values from the table.
- ☐ **B2. `GainExperience(player, amount)`**, porting `gain_exp`:
  - per-gain cap
  - messages ("You receive N experience points." / "1 lousy experience point")
  - level-up when over `needed`, then XP resets to 0
  - negative gains clamp at 0 and print "You lose N experience points."
  - no gain at 110+
- ☐ **B3. `AdvanceLevel(player)`**, porting `advance_level`: HP/mana/move gains by class group, attribute improvement
  capped by race maximum, the level-up message, and a save.
  Return a result record so callers and tests can see what changed.
- ☐ **B4. Titles.** Build "the {Race} {Title}" from the class table using sex for male/female, as `set_title` does.
  Respect a future NOTITLE flag; for now, always set it on level-up.

### C. Wire XP into the game

- ☐ **C1. Mob kill XP:** replace `mob.Level * 100` with `GainExperience(killer, max(1, mob.Experience / 4))`.
- ☐ **C2. Damage XP:** stop giving it in PvP. Keep the legacy "add directly" behavior and leave a comment saying it's intentional.
  Unify the four places that currently do this (`GameTickService` ×2, `MeleeAttackExecutor` ×2) into one method.
- ☐ **C3. PvP kill XP:** match legacy (`victim.exp / 4`) instead of `level * 100`.
- ☐ **C4. Death penalty:** `GainExperience(victim, -ExpNeeded / 2)`.
- ☐ **C5. Flee:** route the XP loss through `GainExperience` so the messages match.
- ☐ **C6. Instant-kill skills** (backstab, kick, bash) use the same kill-reward path. Today they duplicate the logic.

### D. Character creation

- ☐ **D1. Starting vitals from the race table** (`hp_start`, `mn_start`, `mv_start`) instead of 20/100/100.
- ☐ **D2. Starting title** set at creation (level-1 title).
- ☐ **D3. Clamp attributes to race maxima** wherever effective stats are calculated (legacy `affect_total`).
- ~~D4. Stat rolling~~: deferred (see Decisions).

### E. Visible to the player

- ☐ **E1. `score`:** real "exp to next level" from `ExpNeeded`, plus the title line.
- ☐ **E2. `who` / `look`:** show the title.
- ☐ **E3. `stat`:** show practices once they exist.
- ☐ **E4. `setlevel`** (test command) goes through `AdvanceLevel` repeatedly, so gains apply when jumping levels.

### F. Practices (groundwork only)

- ☐ **F1.** Add a `Practices` field to `PlayerState` plus a DB column (additive migration), and award practices on level-up.
  The `practice` command itself waits for guildmasters (separate plan).

### G. Tests

- ☐ **G1.** Unit tests: `ExpNeeded` table lookups, gain cap, level-up rollover, negative clamp, deity cap.
- ☐ **G2.** Unit tests: `AdvanceLevel` per class group (HP divisor, mana) using a seeded random.
- ☐ **G3.** Integration test: kill a mob with known XP, assert the gain and a level-up.
- ☐ **G4.** Persistence test: level, XP, title and practices survive save/load.

---

## Out of Scope (for now)

- Group XP sharing (needs groups)
- Dual/multi-class levelling (`LOWEST_CLASS`, divided gains)
- Remort
- Immortal levels and autowiz
- The `practice` command and guildmasters
- Clan level tracking

## Decisions

- **Mortal level cap:** 109. Levels 110+ are immortal only. (Oct 10, 2026)
- **Stat rolling:** deferred. The architecture isn't settled yet; keep the fixed 11s for now. (Oct 10, 2026)
- **Multi-class / dual-class:** deferred, same reason. (Oct 10, 2026)

**Keep in mind for later:** don't design anything that assumes a player has exactly one class and one level.
Write the XP and level-up functions to take `(class, level)` as inputs rather than reading `player.Class`,
so multi-class can later call them per class slot (legacy uses `LOWEST_CLASS` / `LOWEST_LEVEL`).

## Data Design Requirements

Draft data model with diagrams: <https://claude.ai/artifact/GxD5feffnUfeg3i3iNmAGA>

Agreed so far (Oct 10, 2026):

- **JSON content**, one file per thing (`content/classes/paladin.json`, `content/races/human.json`).
- **No enums.** Classes and races are identified by string ids. Behavior comes from data, not `switch` statements.
- **One schema per kind.** Every file of a kind follows the same JSON Schema, referenced via `$schema`.
- **Formulas are Lua and hot-reloadable**, so skills, spells and progression can be rebalanced without a restart.
- **References must never fail silently.** A typo must not quietly make a class unplayable or an item unobtainable:
  1. **Schema enums:** generated schemas list the valid ids, so editors flag typos. A test fails if the schemas are stale.
  2. **Resolve at load:** every reference field is typed as a reference to a kind. An unknown id fails the whole load,
     with file, JSON path and a "did you mean" hint. Boot refuses to start; reload keeps the old data.
  3. **Typed model:** after load, C# holds definition objects (`IReadOnlyList<ClassDefinition>`), never id strings.
  4. **Content test:** one test loads all of `content/` through the same pipeline.
  5. **Reachability warnings:** report things nothing references (a playable class no race allows, a skill no class
     learns, an object nothing loads). Entries can opt out as intentionally unreferenced.
  - Ids are exact matches in the format `^[a-z][a-z0-9-]*$`. No case-insensitive or fuzzy matching.

## Open Questions

See the open questions on the data model page: skill ownership, generated vs hand-written schemas,
explicit XP values vs interpolation, formula variables, reload behavior for online players, and reload scope.
