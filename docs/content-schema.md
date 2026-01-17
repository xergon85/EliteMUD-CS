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
- `zoneId` maps to the legacy zone table index; use the zone number from the `.zon` header.
- `sector` maps to the legacy `SECT_*` enums.
- `flags` maps to legacy `room_flags` bits.
- `exitFlags` maps to legacy `EX_*` bits.

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
