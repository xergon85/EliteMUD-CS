# Legacy Import Audit

_Date: Oct 10, 2026 · Scope: `zones/*.json` (115 files) compared with the legacy files in `~/Dev/elitemud/lib/world`_

**Why:** We want strict reference checking for world content (rooms, mobs, objects, zones), like the plan for classes and races
(see `docs/plans/levelling-and-classes.md`). Before switching that on, we need to know how broken the current data is.

## Summary

- **About 570 broken references** in the imported world data. The server ignores all of them silently today.
- **Most of them are importer bugs, not bad legacy data.** Three bugs account for the bulk:
  1. **Tabs break zone reset parsing:** 485 reset lines are mangled. Example: 244 mob spawns lose their room, so those mobs never appear (including Midgaard shopkeepers).
  2. **Mob records are silently dropped:** 102 mobs exist in the legacy files but weren't imported. Some files stop importing part way through.
  3. **Objects with no matching zone file are dropped:** 464 objects exist in the legacy files but weren't imported.
- **Some breakage is real legacy data**, e.g. an exit to room 4474912. This needs fixing or marking by hand once the importer is fixed.

**Recommendation:** fix the importer and re-import *before* building the reference checker. Then run the checker as warnings first, and only make it strict once the remaining legacy problems are fixed or marked.

## Progress (Oct 11, 2026, branch `fix/legacy-import`)

| Finding | Status | Commits |
|---|---|---|
| 1. Tabs break reset parsing | ✅ Fixed. Reset lines are parsed like `load_reset_cmds`. All 478 mangled resets restored, comments kept. | `de561a0`, data `3da2761` |
| 2. Mob records dropped | ✅ Fixed. All 2,658 legacy mob records import; 38 more mobs in the zones. The causes were parser differences, not unknown formats (see below). | `976a25e`, data `be6be66` |
| 3. Zone-less objects dropped | ✅ Not a bug. All 2,827 objects are imported and loaded from `content/objects/objects.json`; the audit missed that file. The real question, which objects nothing creates, is answered in `unsourced-objects-2026-10-11.md`. | |
| 4. Broken references | 🔶 Partly. Rooms swallowed by misparsed room programs are recovered (20 rooms), including 7716, 12951, and the bogus exit to 4474912. Not re-measured as a whole yet. | `15d0ff1`, data `f94b678` |
| 5. Server hides problems | 🔶 Importer now logs unparseable reset lines and unknown room markers. Server side unchanged. | |

Also done: the in-game `import-legacy` command was removed; importing is CLI-only (`bce5a6c`).

**What actually caused the dropped mobs and rooms.** The importer read the files differently from the original C code in four places:

- **Room markers** were matched case-insensitively with `StartsWith("D")`, and programs only on an exact `>`. Real programs look like `>push 10 1~`, so the program text was read word by word, and "down" became an exit that swallowed the next room. Now ported from `load_rooms`.
- **Tilde strings** ended at any `~`. Legacy `fread_string` ends only on a line whose last non-space character is `~`, and discards the rest of that line. A stray `~~` (93.mob #9308) shifted every following field.
- **Dice** like `3d6+9-1` (terminator glued on, 5.mob #501) were read as one token.
- **List values** like `385-1` (terminator glued on) were parsed as flag bits.

**New findings while fixing:**

- **Hand-fix list:** ~80 import warnings are genuine errors in the legacy files that the original also misreads, e.g. text placed before an exit marker (66.wld #6644), stray `~` lines, the wrapped zone comment `request` (26.zon) and `O o 3034 …` (289.zon). Because of the last one, the clan hall ATM (object 3034, room 28906) no longer loads; the old importer read the typo as `0` by accident.
- **Duplicate ids:** 65 room ids and 12 mob ids are defined twice in the legacy files. Legacy keeps the later definition; we haven't checked which one we keep. Separately, 378 room ids appear in more than one grouped zone file (this predates the fixes and comes from the grouping step).
- **Not bugs:** the color-coded door keywords in clan zone 275 are intentional builder work. The mob with id 0 ("Noname Monster") is a real legacy record in `0.mob`.

---

## Finding 1: Tabs break zone reset parsing

**Cause:** `LegacyImportUtilities.SplitTokens` splits only on spaces:

```csharp
// src/EliteMud.Legacy/Import/LegacyImportUtilities.cs:23
return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
```

Legacy zone files often put a tab and a comment after the last number:

```
M 0 3031 1 3072		Jeweler        (legacy 30.zon, line 97)
```

The last token becomes `"3072\t\tJeweler"`, so `int.TryParse` fails:
- `Arg3` turns into `null` (`LegacyZoneImporter.cs:80`)
- `Arg1`/`Arg2` turn into `0`, because `LegacyImportLookup.ParseInt` returns 0 on failure

**What is lost** (counted by re-parsing every legacy `.zon` file both ways):

| Reset | Lines mangled | Effect in game |
|---|---|---|
| `M` (load mob) | 244 | Mob has no room, so it never spawns |
| `E` (equip mob) | 145 | Equipment slot lost, so mobs spawn without that gear |
| `G` (give mob) | 49 | Spawn chance/max becomes 0 |
| `D` (door state) | 32 | Door state lost, so doors that should be locked aren't |
| `O` (load object) | 9 | Object has no room |
| `P` (put in container) | 3 | Container lost |
| `R` (remove object) | 3 | Room lost |
| **Total** | **485** | |

**Concrete example:** in Northern Midgaard, the jeweler (3031), Rosy (3032) and other shopkeepers have `Arg3: null` in
`zones/zone_030_Northern Midgaard Main City.json`, although the legacy file names rooms 3072 and 3073.

**Second symptom:** reset comments are lost too. `ExtractComment` looks for `*`, but most legacy comments follow a tab without one.

**Fix:** split on any whitespace (`line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)`), read only the
number of arguments each command takes, and treat the rest of the line as the comment. Log a warning when a token
expected to be a number isn't one, instead of defaulting to 0 or null.

## Finding 2: Mob records silently dropped

102 mobs exist in `lib/world/mob/*.mob` but not in the imported zones. The two patterns:

| Pattern | Example | What it looks like |
|---|---|---|
| Rest of file dropped after an unusual record | `93.mob` (Galaxy): 32 mobs, only 9301–9308 imported | Mob 9309 (Hercules) uses a short stat line (`0 4 66 128 1000 A 84 1`). Everything after it is missing. |
| Individual records skipped | `5.mob` (Northern Woods): 502, 505–507, 511 missing | Mob 502 uses letter flags (`3 0 bgh y 0 S`) and a `%%3 hop~` line, both of which the importer doesn't seem to handle |

Biggest losses by file: `93.mob` 24, `154.mob` 20, `5.mob` 5, `30.mob` 5, `120.mob` 5.

Knock-on effect: zone resets that load these mobs point at nothing. That's 67 "missing mob" references. The `E`/`G` resets that follow them (e.g. objects 9301, 9305 in Galaxy) also fail.

**Fix:** find out which legacy mob format variants exist (`db.c` mob parsing, via the cscope index). Port the parser for each variant.
Never skip a record without logging the file, record number and reason.

## Finding 3: Objects without a zone file dropped

> **Correction (Oct 11, 2026):** these objects are not dropped. The server also loads `content/objects/objects.json`,
> which holds all 2,827 legacy objects. The 464 are objects that no zone file contains. Which of them nothing in the
> world creates (quest, clan and admin items, plus 18 door keys and 28 portals to check) is covered in
> `docs/reports/unsourced-objects-2026-10-11.md`.

464 objects exist in `lib/world/obj/*.obj` but weren't imported. Most come from `.obj` files that have **no matching `.zon` file**:

| File | Objects lost | Has `.zon`? |
|---|---|---|
| `24.obj` | 69 | no |
| `990.obj` | 68 | no |
| `11.obj` | 27 | no |
| `19.obj` | 27 | no |
| `170.obj` | 31 | yes (needs a look) |
| `1.obj`, `2.obj` | 27 each | yes (needs a look) |

The grouped format puts objects inside a zone file. Objects with no zone have nowhere to go, so they disappear. Some of
these are probably referenced elsewhere: door key 1007 (in `10.obj`) is used by the Clan Zone doors.

**Fix:** decide where zone-less objects live. One option is the existing `content/objects/objects.json` for shared objects.
Also check why files that *do* have a zone (170, 1, 2) still lose objects.

## Finding 4: Broken references in the imported data

From a reference check over everything that was imported:

| Broken reference | Count | Example | Likely cause |
|---|---|---|---|
| Mob reset → missing room | 257 | Midgaard shopkeepers 3031–3033 | Finding 1 |
| Door key → missing object | 92 | Clan Zone room 239, key 1007 | Finding 3, maybe some real |
| Exit → missing room | 89 | Sekky's PK zone, room 7715 North → 4474912 | Mixed. 7716 exists in legacy `77.wld` but wasn't imported; 4474912 is real bad data |
| Mob reset → missing mob | 67 | Northern Woods mob 502 | Finding 2 |
| Object resets (O/G/E/P) → missing object or container | ~50 | Galaxy 9301; Honeymoon Resort 22900+ | Findings 1–3; 22900+ don't exist in legacy either |
| Door reset → missing room | 4 | Merkin village room 12951 | To check |

Rooms: 18 legacy rooms weren't imported (9 of them from `129.wld`). 137 imported rooms have no exit leading into them. Some of those are intentional, like god rooms and teleport targets.

## Finding 5: The server hides all of this

- `WorldState.ExecuteLoadObject` / `ExecuteLoadMob` return early when an id isn't found. Nothing is logged.
- `ContentLoader` skips a zone with no `Zone` block, also without a log (already on the roadmap as a known issue).
- The importer turns unparseable numbers into `0` or `null`.

Each layer quietly passes broken data on. That's how a shopkeeper can be missing from Midgaard with no error anywhere.

---

## Suggested Order

1. ✅ **Fix the tokenizer** (Finding 1). Small change, biggest effect. Add an importer test using real legacy lines with tabs.
2. 🔶 **Make the importer loud.** Every skipped record or unparseable field is logged with file, record and reason, and summarized at the end. *(Reset lines and room markers done; mobs, objects and a summary still to do.)*
3. ✅ **Fix mob format variants** (Finding 2). ✅ **Zone-less objects** (Finding 3): already loaded from the shared file; see the unsourced objects report.
4. ⬜ **Re-import and re-run this audit.** What remains should be real legacy breakage.
5. ⬜ **Build the reference checker** for world content, starting in warning mode. Fix or mark what's left, then switch to errors.

## Method

- **Legacy side:** every `.zon`/`.mob`/`.obj`/`.wld` file under `~/Dev/elitemud/lib/world` (Latin-1). Record ids are taken from `#<number>` lines; the `removed/` folder is excluded.
- **Imported side:** `zones/*.json` plus `content/objects/objects.json`.
- **Reset check:** for each reset command, verify the mob, object and room ids it refers to exist (`M`, `O`, `G`, `E`, `P`, `D`, `R`).
- **Tokenizer check:** re-parse every legacy reset line with a space-only split and with a whitespace split, and count the lines where the space-only split yields a non-number in an argument position.
- **Caveats:**
  - Counts are approximate. A few legacy lines are false positives, e.g. a zone name starting with "R".
  - Imported rooms (7,073) outnumber legacy rooms found (6,818). That's probably test zones or the regex, and wasn't investigated.
  - The audit script was a one-off, not committed.
