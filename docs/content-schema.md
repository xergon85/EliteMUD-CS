# World Content Schema (v1)

This schema stores game content as versioned files so Git can track changes. The runtime loads these files to build the world state. Player/runtime data remains in SQLite.

## Directory Layout
```
content/
  rooms/
    rooms.json
  mobs/
    mobs.json
  objects/
    objects.json
  zones/
    zones.json
  scripts/
    scripts.json
  skills/
    skills.json
  classes/
    <id>.json          one file per class
  races/
    <id>.json          one file per race
  progression/
    xp-curves/<id>.json
    level-up/<id>.json
  meta/
    schema.json
```

## Common Conventions
- All entities include a numeric `id` that matches legacy vnums where applicable.
- All files use UTF-8 encoded JSON.
- `schema.json` declares schema version and optional build metadata.
- Bitfields in legacy data are stored as named flags.

## Schema Metadata
**File:** `content/meta/schema.json`
```json
{
  "version": 1,
  "createdUtc": "2026-01-17T00:00:00Z",
  "notes": "Initial schema for rooms/mobs/objects/zones/scripts."
}
```

## Rooms
**File:** `content/rooms/rooms.json`
```json
{
  "rooms": [
    {
      "id": 1,
      "name": "The Entry Hall",
      "description": "A simple stone hall with torchlight flickering along the walls.",
      "zoneId": 0,
      "sector": "Inside",
      "flags": ["Indoors"],
      "exits": [
        {
          "direction": "North",
          "targetId": 2,
          "description": "",
          "keywords": [],
          "exitFlags": ["IsDoor"],
          "keyId": null
        }
      ],
      "extraDescriptions": [
        {
          "keywords": ["torch", "light"],
          "description": "The torches burn steadily."
        }
      ],
      "specialProc": null,
      "roomPrograms": [],
      "crashRoom": false
    }
  ]
}
```

### Notes
- `zoneId` is the legacy zone number (from the `.zon` header), not the table index.
- `sector` maps to the legacy `SECT_*` enums.
- `flags` maps to legacy `room_flags` bits.
- `exitFlags` maps to legacy `EX_*` bits.
- `targetId` and `keyId` are legacy vnums (not rnums).

## Mobs (NPCs)
**File:** `content/mobs/mobs.json`
```json
{
  "mobs": [
    {
      "id": 1001,
      "name": "training dummy",
      "shortDescription": "a worn training dummy",
      "longDescription": "A worn training dummy stands here.",
      "description": "Stuffed with straw and stitched roughly.",
      "level": 1,
      "race": "Human",
      "class": "Warrior",
      "flags": ["Sentinel"],
      "affects": ["DetectInvis"],
      "alignment": 0,
      "stats": {
        "strength": 10,
        "dexterity": 10,
        "intelligence": 10,
        "wisdom": 10,
        "constitution": 10,
        "charisma": 10
      },
      "resources": {
        "hitDice": "1d1+1",
        "mana": 100,
        "move": 80
      },
      "combat": {
        "armor": 100,
        "hitroll": 1,
        "damroll": 1
      },
      "attacks": [
        {
          "type": "Hit",
          "damageType": 500,
          "chance": 100,
          "damageDice": "1d1+1"
        }
      ],
      "skills": [],
      "resistances": [],
      "gold": 0,
      "experience": 0,
      "defaultPosition": "Standing",
      "sex": "Neutral",
      "actionScript": null,
      "specialProc": null,
      "programs": []
    }
  ]
}
```

### Notes
- `flags` maps to legacy `MOB_FLAGS` bits.
- `affects` maps to legacy `affected_by` bits.
- Mob format is normalized across legacy `S`, `A`, and classic layouts.

## Objects
**File:** `content/objects/objects.json`
```json
{
  "objects": [
    {
      "id": 2001,
      "name": "practice sword",
      "shortDescription": "a practice sword",
      "longDescription": "A practice sword rests here.",
      "description": "Blunted steel with a wrapped grip.",
      "type": "Weapon",
      "level": 1,
      "antiClass": "None",
      "extraFlags": ["NoDrop"],
      "wearFlags": ["Wield"],
      "values": [1, 4, 0, 0, 0, 0],
      "weight": 3,
      "cost": 10,
      "costPerDay": 0,
      "extraDescriptions": [
        {
          "keywords": ["sword"],
          "description": "The edge is blunted from practice."
        }
      ],
      "affects": [
        { "location": "Strength", "modifier": 1 }
      ],
      "bitvectors": [],
      "specialProc": null
    }
  ]
}
```

### Notes
- `extraFlags` maps to legacy `extra_flags` bits.
- `wearFlags` maps to legacy `wear_flags` bits.

## Zones (Resets/Spawns)
**File:** `content/zones/zones.json`
```json
{
  "zones": [
    {
      "id": 10,
      "name": "Training Grounds",
      "topRoomId": 1099,
      "lifespan": 60,
      "resetMode": "ResetAlways",
      "resetCommands": [
        {
          "command": "M",
          "ifFlag": 0,
          "arg1": 1001,
          "arg2": 1,
          "arg3": 2,
          "comment": "Load training dummy"
        }
      ]
    }
  ]
}
```

### Notes
- `resetMode` maps to legacy 0/1/2 values.
- `resetCommands` preserves the legacy tuple; loader can translate into typed actions.
- Optional `expanded` data can be added later for readability while keeping raw tuples.
- `ifFlag: 1` means "only run if the previous reset command succeeded" (legacy `last_cmd` in `reset_zone`).

### Grouped zone files (`zones/zone_NNN_Name.json`)
The server loads these. Each has a `Zone` block plus the zone's `Rooms`, `Mobs` and `Objects`.
Extra fields in the `Zone` block:
- `Active` (bool, default `true`): `false` keeps the zone's data but the server skips it. Zones the original
  game didn't load are imported as inactive (Clan Zone 1 and 2, the Dark Legion hall, Honeymoon Resort, Wedding
  Chapel). Set it to `true` to switch a zone on.
- `RoomRange` (`Min`, `Max`): the rooms the zone owns. Used to find a room's zone (e.g. `zreset` without a number)
  and to clear the zone's rooms on reset. Active zones use the legacy rule: from the previous zone's top room + 1
  up to their own `TopRoomId`.
- `SourceFile`: the legacy file the zone was imported from, e.g. `zon/66.zon`.

### Legacy import decisions (`content/meta/legacy-import.json`)
The importer (`src/EliteMud.Cli`) follows the legacy `index` files, which list the files the original game loaded.
This config covers what the index can't decide:
- `skip`: files not to import at all, each with a reason (old copies of zones, e.g. Galaxy 93 and Gnome City 15).
- `activeZones`: zones missing from the snapshot's index that are live anyway (none at the moment; New Thalos turned
  out to be unfinished).
- `fixes`: corrections to legacy data, applied after parsing, each with a reason, e.g. setting an exit's door type.
  Every applied fix is written to `import-log.txt`.

### Content check (`content/meta/accepted-findings.json`)
The server checks the loaded world at startup and prints a summary (warning mode).
`dotnet run --project src/EliteMud.Server -- --validate` prints every finding and exits with the number of errors.
It reports broken references (exits, door keys, every reset type, portals), door keys nothing creates, and zones that
can't be reached from the Temple. Findings that were looked at and accepted go in `accepted-findings.json`, each with
a reason; an entry matches by category plus text in the location and message. An entry that matches nothing is
reported. A test fails on any open error or warning.

Each import writes `import-log.txt` next to its output, listing every skipped file, duplicate id and malformed
record, and prints a summary.

### Reset commands
| Command | Args (`arg1`, `arg2`, `arg3`) | Meaning | Server support |
|---|---|---|---|
| `M` | mob, max existing, room | Load a mob | Yes |
| `O` | object, spawn chance %, room | Load an object into a room | Yes |
| `G` | object, spawn chance % | Give an object to the last mob | Stub |
| `E` | object, spawn chance %, wear slot | Equip the last mob | Yes |
| `P` | object, spawn chance %, container object | Put an object in a container | Stub |
| `D` | room, direction, state (0 open, 1 closed, 2 locked) | Set one side of an exit's state. Works on any exit; closing a non-door makes a barrier that players can't open | Yes |
| `R` | room, number of exits | Shuffle the room's first N exits (mazes) | Yes |
| `L` | room, portal object, destination room | Load a portal (or reuse the one there) and set its destination | Yes |
| `A` | room, portal flags, key object | Set the room's portal state (1 closed, 2 locked) and the object needed to `activate` it (-1 none) | Yes |

### Portal objects
Objects of type `Portal` use their `values` as: `[0]` destination room, `[1]` flags (1 closed, 2 locked),
`[2]` key object needed to `activate` it (-1 none), `[3]` minimum level, `[4]` maximum level (-1 none),
`[5]` mud hours an activated portal stays open (-1 forever). Players use `enter <portal>` and `activate <portal>`.

## Scripts
**File:** `content/scripts/scripts.json`
```json
{
  "scripts": [
    {
      "id": "entry-hall-look",
      "hook": "OnLook",
      "when": { "roomId": 1 },
      "body": "emit('Dust motes drift lazily in the torchlight.')"
    }
  ]
}
```

## Skills and Spells
**File:** `content/skills/skills.json`

This file defines metadata for all skills and spells in the game, including mechanics, class restrictions, and formulas.

```json
{
  "version": 1,
  "description": "Skill and spell metadata for EliteMUD",
  "skills": [
    {
      "id": 323,
      "name": "kick",
      "aliases": [],
      "description": "A powerful kick attack that can start or continue combat",
      "type": "Active",
      "category": "Combat",
      "minimumLevel": 1,
      "waitStateRounds": 3,
      "skillgainCooldown": 60,
      "classRestrictions": [
        {
          "class": "Warrior",
          "minLevel": 1,
          "maxProficiency": 95,
          "difficulty": 10
        },
        {
          "class": "Thief",
          "minLevel": 18,
          "maxProficiency": 95,
          "difficulty": 10
        }
      ],
      "mechanics": {
        "damageFormula": "level / 2",
        "hitFormula": "return ((10 - div(victimAC, 10)) * 2) + random(1,101) <= skillPercent",
        "requirements": [
          {
            "type": "position",
            "value": "Fighting",
            "message": "You can't kick while sitting down!"
          }
        ],
        "effects": []
      }
    }
  ]
}
```

### Field Descriptions

#### Root Level
- `version` - Schema version (integer)
- `description` - Human-readable description
- `skills` - Array of skill/spell definitions

#### Skill Definition
- `id` - Numeric skill ID (matches `SkillType` enum value, e.g., 323 for Kick)
- `name` - Canonical skill name (lowercase, e.g., "kick", "backstab")
- `aliases` - Alternative command names (e.g., `["bs"]` for backstab)
- `description` - Human-readable description shown to players
- `type` - Skill type: `"Active"` or `"Passive"`
- `category` - Skill category: `"Combat"`, `"Stealth"`, `"Defensive"`, `"Support"`, etc.
- `minimumLevel` - Base minimum level to learn (before class modifiers)
- `waitStateRounds` - Rounds of WAIT_STATE applied after use (0 for passive skills)
- `skillgainCooldown` - Seconds between skill improvements (typically 60)

#### Class Restrictions
Each skill has an array of 20 class restriction entries (one per class):
- `class` - Class name (e.g., `"Warrior"`, `"Thief"`, `"MagicUser"`)
- `minLevel` - Minimum level to learn this skill (`null` if class cannot learn)
- `maxProficiency` - Maximum skill percentage (0-100, typically 95)
- `difficulty` - Practice improvement rate (typically 10)

**Class Order**: MagicUser, Cleric, Thief, Warrior, Psionicist, Monk, Bard, Knight, Wizard, Druid, Assassin, Ranger, Illusionist, Paladin, Mariner, Cavalier, Unused17, Unused18, Ninja, Unused20

#### Mechanics (Active Skills)
- `damageFormula` - Damage calculation (e.g., `"level / 2"`, `"10"`)
- `damageMultiplierFormula` - Damage multiplier (e.g., `"MIN(level / 10 + 1, 5)"` for backstab)
- `hitFormula` - Hit chance calculation (e.g., `"random(1,101)"`)
- `requirements` - Array of requirement objects (position, equipment, victim state)
- `effects` - Array of effect objects (position changes, combat redirection, etc.)

#### Mechanics (Passive Skills)
- `activationFormula` - When skill activates (e.g., `"(random(1,250) + damage) < skillPercent"`)
- `effectFormula` - Effect calculation (e.g., `"damage - (level * 2)"`)
- `requirements` - Array of requirement objects
- `effects` - Array of effect objects

#### Requirement Object
- `type` - Requirement type: `"position"`, `"equipment"`, `"victimState"`
- `value` - Required value (e.g., `"Fighting"`, `"Shield"`, `"notFighting"`)
- `message` - Error message if requirement not met
- `implemented` - Boolean flag (false if requirement check not yet coded)

#### Effect Object
- `type` - Effect trigger: `"onHit"`, `"onMiss"`, `"onSuccess"`, `"victimAsleep"`
- `target` - Effect target: `"self"`, `"victim"`, `"ally"`
- `effect` - Effect name: `"setPosition"`, `"waitState"`, `"redirectCombat"`, `"autoHit"`
- `value` - Effect value (position name, rounds, boolean)
- `description` - Human-readable effect description

### Notes
- Formulas use string expressions for flexibility (can be parsed/evaluated at runtime)
- All 20 class entries are stored even if `minLevel` is `null` (for easy lookup)
- Equipment requirements (shield, weapon type) are defined but may not be enforced until equipment system is complete
- Legacy skill IDs from EliteMUD constants.c are preserved (Spells: 0-299, Skills: 300-399)
- Skills without implemented mechanics (e.g., Tumble) have `"NOT_YET_IMPLEMENTED"` placeholders

### Formulas
- Formulas are Lua and must `return` a value: a number, or true/false for checks (e.g. `hitFormula`).
- **Variables:** each formula slot gets a fixed set of variables, in camelCase, from its input record
  (`src/EliteMud.Application/Formulas/FormulaInputs.cs`, or a record nested in the skill/spell class):

  | Inputs | Variables | Used by |
  |---|---|---|
  | `LevelInputs` | `level` | damage, healing, duration and bonus formulas |
  | `SkillCheckInputs` | `skillPercent` | skill hit checks |
  | `SpellCheckInputs` | `level`, `spellPercent` | spell success checks |
  | `DefenseCheckInputs` | `damage`, `skillPercent` | dodge/parry activation |
  | `DefenseEffectInputs` | `damage`, `level` | dodge/parry effect |
  | `KickSkill.HitInputs` | `victimAC`, `skillPercent` | kick hit check |
  | `LevelUpInputs` | `level`, `str`, `int`, `wis`, `dex`, `con`, `cha` | level-up gains (`level` is before advancing) |
  | `NoInputs` | none | constant formulas, e.g. `return 10` |
- **Built-ins:** `math` (Lua's), `random(min, max)` (inclusive, like legacy `number()`), and `div(a, b)`: integer
  division truncating toward zero, like C. Lua's `/` is real division, so use `div` where legacy C divides integers
  (e.g. kick: `div(victimAC, 10)`; -55 / 10 is -5 in C, -5.5 with `/`).
- **Sandbox:** formulas can only use their variables and the built-ins; there's no file, OS or code-loading access, and
  `math` is read-only.
- **Checked at startup:** every name a formula reads must be one of its variables or a built-in, checked in every branch
  without running it. Then one sample run catches syntax errors and the result type of the branch it takes. Any
  problem stops the server with a message naming the formula; `--validate` reports them too. The result type of
  branches the sample doesn't take isn't guaranteed; the content editor's live preview covers that.


## Classes, Races and Progression

Generated from the legacy source by `elitemud-import rules <legacy> <content>` (log in
`content/meta/legacy-rules-import-log.txt`), then edited as content. One file per item; the file name is the `id`.
Loaded by `RulesContentLoader` into one `RulesContent` snapshot. Every problem below stops the server at startup and
is listed by `--validate`.

### Class (`content/classes/<id>.json`)
| Field | Type | Notes |
|---|---|---|
| `id` | string | e.g. `paladin` |
| `name`, `abbreviation` | string | abbreviation keeps legacy colour codes (`#bPa#N`) |
| `legacyNumber` | int | the legacy CLASS_ number, for importing player files |
| `menuKey` | string | letter picked at character creation (legacy: `'a'` + class number − 1); unique |
| `xpCurve` | string | id of an XP curve; must exist |
| `levelUpProfile` | string | id of a level-up profile; must exist |
| `primeStats` | string[] | `str`, `int`, `wis`, `dex`, `con`, `cha` |
| `strengthBonus` | bool | gets exceptional strength (18/xx) |
| `titles` | `{male, female}`[] | exactly 40 rows, one per 3 levels as in legacy `titles[][]` |

### Race (`content/races/<id>.json`)
| Field | Type | Notes |
|---|---|---|
| `id`, `name`, `titleName`, `legacyNumber` | | as for classes |
| `availability` | string | `creation`, `granted`, `remort`, `unused` or `unknown` |
| `availabilityNote` | string? | how a non-creation race is obtained |
| `menuKey` | string? | letter picked at character creation (legacy race menu); required and unique for `creation` races, absent for the others |
| `statMax` | `{str, int, wis, dex, con, cha}` | racial stat maximums |
| `start` | `{hp, mana, move}` | starting points |
| `allowedClasses` | string[] | class ids; each must exist |
| `averages` | `{age, heightInches, weightPounds}` | |

### XP curve (`content/progression/xp-curves/<id>.json`)
- `thresholds`: XP needed for each level 1–109, exactly 109 values (index 0 = level 1).
- `legacyRows`: the legacy XP column the thresholds were generated from (reference only).
- `classes`: the classes using this curve. Must match the classes whose `xpCurve` is this id.

### Level-up profile (`content/progression/level-up/<id>.json`)
- `hpGain`, `manaGain`, `moveGain`: number formulas with `LevelUpInputs` (see Formulas).
- `source`: where in legacy the formulas come from.
- `classes`: as for XP curves, must match the classes using this profile.

## Flag Maps (v1)

### Room Flags
- `Dark`, `Death`, `NoMob`, `Indoors`, `Lawful`, `Neutral`, `Chaotic`, `NoMagic`,
  `Tunnel`, `Private`, `GodRoom`, `ZeroMana`, `Dispell`, `Silent`, `InAir`,
  `PkOk`, `Arena`, `Regen`, `NoTeleport`, `NoScry`, `NoFlee`, `Damage`,
  `NoTrack`, `NoSweep`, `NoScout`, `NoSleep`, `NoSummon`, `NoQuit`, `NoDrop`

### Exit Flags
- `IsDoor`, `Closed`, `Locked`, `ResetClosed`, `ResetLocked`, `PickProof`,
  `Trap`, `Wall`, `BashProof`, `MagicProof`, `PassProof`, `TrapSet`, `Secret`, `Broken`

### Sector Types
- `Inside`, `City`, `Field`, `Forest`, `Hills`, `Mountain`, `WaterSwim`,
  `WaterNoSwim`, `Underwater`, `Air`, `Void`, `Desert`, `FoulWaste`,
  `FoulMountain`, `IcyUnderwater`, `FoulWaterNoSwim`

### Mob Flags
- `IsNpc`, `Sentinel`, `Scavenger`, `IsAggressive`, `StayZone`, `Wimpy`,
  `AggressiveEvil`, `AggressiveGood`, `AggressiveNeutral`, `Memory`, `Helper`,
  `NoCharm`, `NoSummon`, `NoSleep`, `NoBash`, `NoBlind`, `Hunter`, `Murderer`,
  `Wielding`, `AggressiveNeutral2`, `AggressiveGood2`, `AggressiveEvil2`,
  `AggressiveCleric`, `AggressiveMage`, `AggressiveThief`, `AggressiveWarrior`

### Affect Flags
- `Blind`, `Invisible`, `DetectAlign`, `DetectInvis`, `DetectMagic`,
  `SenseLife`, `Sanctuary`, `Group`, `Curse`, `Light`, `Poison`,
  `ProtectEvil`, `Sleep`, `Sneak`, `Hide`, `Charm`, `Infrared`,
  `Berserk`, `Hover`, `Fly`, `BreathWater`, `Regeneration`

## Future Extensions
- Split large files into sharded chunks per zone/area.
- Add localization dictionaries for multi-language text.
- Attach binary assets via `assets/` with manifest references.
