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
        "hitFormula": "((10 - victimAC/10) * 2) + random(1,101)",
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
